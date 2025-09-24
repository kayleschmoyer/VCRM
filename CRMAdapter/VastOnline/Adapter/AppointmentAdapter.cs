/*
 * File: AppointmentAdapter.cs
 * Role: Implements canonical appointment access for the Vast Online backend.
 * Architectural Purpose: Normalizes scheduling data from Azure SQL into the CRM canonical appointment aggregate.
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
        /// <param name="defaultListLimit">Optional default list limit.</param>
        public AppointmentAdapter(DbConnection connection, FieldMap fieldMap, int defaultListLimit = DefaultListLimit)
            : base(connection, fieldMap)
        {
            _defaultListLimit = defaultListLimit;
            MappingValidator.EnsureMappings(fieldMap, RequiredAppointmentKeys, nameof(AppointmentAdapter));
        }

        /// <inheritdoc />
        public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fieldMap = FieldMap.GetTargets("Appointment", AppointmentProjectionFields);
            var source = FieldMap.GetEntitySource("Appointment");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap["Id"]} = @id";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return ReadAppointment(reader);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<Appointment>> GetByDateAsync(
            DateTime date,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            var limit = Math.Min(_defaultListLimit, maxResults > 0 ? maxResults : _defaultListLimit);
            var fieldMap = FieldMap.GetTargets("Appointment", AppointmentProjectionFields);
            var source = FieldMap.GetEntitySource("Appointment");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var startField = fieldMap["Start"];
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            var commandText = $@"SELECT TOP (@limit) {selectClause}
FROM {source}
WHERE {startField} >= @start AND {startField} < @end
ORDER BY {startField};";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@limit", limit, DbType.Int32);
            AddParameter(command, "@start", startOfDay, DbType.DateTime2);
            AddParameter(command, "@end", endOfDay, DbType.DateTime2);

            var appointments = new List<Appointment>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                appointments.Add(ReadAppointment(reader));
            }

            return new ReadOnlyCollection<Appointment>(appointments);
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
