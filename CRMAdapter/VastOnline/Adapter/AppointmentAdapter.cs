/*
 * File: AppointmentAdapter.cs
 * Purpose: Implements canonical appointment access for the Vast Online backend.
 * Security Considerations: Validates identifiers and time ranges, clamps limits, parameterizes SQL, and executes operations under centralized retry and rate limiting.
 * Example Usage: `var appointments = await adapter.GetByDateAsync(DateTime.UtcNow.Date, 100, cancellationToken);`
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonConfig;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonDomain;
using CRMAdapter.CommonInfrastructure;

namespace CRMAdapter.VastOnline.Adapter
{
    /// <summary>
    /// Vast Online implementation of the <see cref="IAppointmentAdapter"/> contract.
    /// </summary>
    public sealed class AppointmentAdapter : SqlAdapterBase, IAppointmentAdapter
    {
        private const int DefaultListLimit = 100;

        private static readonly string[] RequiredAppointmentKeys =
        {
            "Appointment.__source",
            "Appointment.Id",
            "Appointment.CustomerId",
            "Appointment.VehicleId",
            "Appointment.Start",
            "Appointment.End",
            "Appointment.Advisor",
            "Appointment.Status",
            "Appointment.Location"
        };

        private static readonly string[] AppointmentProjectionFields =
        {
            "Id",
            "CustomerId",
            "VehicleId",
            "Start",
            "End",
            "Advisor",
            "Status",
            "Location"
        };

        private readonly int _defaultListLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppointmentAdapter"/> class.
        /// </summary>
        /// <param name="connection">Database connection.</param>
        /// <param name="fieldMap">Field map configuration.</param>
        /// <param name="retryPolicy">Retry policy implementation.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="rateLimiter">Rate limiter controlling throughput.</param>
        /// <param name="defaultListLimit">Optional default list limit.</param>
        public AppointmentAdapter(
            DbConnection connection,
            FieldMap fieldMap,
            ISqlRetryPolicy? retryPolicy = null,
            IAdapterLogger? logger = null,
            IAdapterRateLimiter? rateLimiter = null,
            int defaultListLimit = DefaultListLimit)
            : base(connection, fieldMap, retryPolicy, logger, rateLimiter)
        {
            if (defaultListLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultListLimit), "Default list limit must be positive.");
            }

            _defaultListLimit = defaultListLimit;
            MappingValidator.EnsureMappings(fieldMap, RequiredAppointmentKeys, nameof(AppointmentAdapter));
            MappingValidator.EnsureEntitySources(fieldMap, new[] { "Appointment" }, nameof(AppointmentAdapter));
        }

        /// <inheritdoc />
        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
            {
                throw new InvalidAdapterRequestException("Appointment id must be provided.");
            }

            return ExecuteDbOperationAsync(
                "AppointmentAdapter.GetById",
                async ct =>
                {
                    var fieldMap = FieldMap.GetTargets("Appointment", AppointmentProjectionFields);
                    var source = FieldMap.GetEntitySource("Appointment");
                    var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
                    var commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap["Id"]} = @id";

                    await using var command = await CreateCommandAsync(commandText, ct).ConfigureAwait(false);
                    AddParameter(command, "@id", id);

                    await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        return null;
                    }

                    return ReadAppointment(reader);
                },
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<Appointment>> GetByDateAsync(
            DateTime date,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            var limit = EnforceLimit(maxResults, _defaultListLimit, nameof(maxResults));
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            return ExecuteDbOperationAsync(
                "AppointmentAdapter.GetByDate",
                async ct =>
                {
                    var fieldMap = FieldMap.GetTargets("Appointment", AppointmentProjectionFields);
                    var source = FieldMap.GetEntitySource("Appointment");
                    var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
                    var startField = fieldMap["Start"];
                    var commandText = $@"SELECT TOP (@limit) {selectClause}
FROM {source}
WHERE {startField} >= @start AND {startField} < @end
ORDER BY {startField};";

                    await using var command = await CreateCommandAsync(commandText, ct).ConfigureAwait(false);
                    AddParameter(command, "@limit", limit, DbType.Int32);
                    AddParameter(command, "@start", startOfDay, DbType.DateTime2);
                    AddParameter(command, "@end", endOfDay, DbType.DateTime2);

                    var appointments = new List<Appointment>();
                    await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        appointments.Add(ReadAppointment(reader));
                    }

                    return (IReadOnlyCollection<Appointment>)new ReadOnlyCollection<Appointment>(appointments);
                },
                cancellationToken);
        }

        private static Appointment ReadAppointment(DbDataReader reader)
        {
            return new Appointment(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetGuid(reader.GetOrdinal("CustomerId")),
                reader.GetGuid(reader.GetOrdinal("VehicleId")),
                reader.GetDateTime(reader.GetOrdinal("Start")),
                reader.GetDateTime(reader.GetOrdinal("End")),
                reader.GetString(reader.GetOrdinal("Advisor")),
                reader.GetString(reader.GetOrdinal("Status")),
                reader.GetString(reader.GetOrdinal("Location")));
        }
    }
}
