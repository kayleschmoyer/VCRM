/*
 * File: VehicleAdapter.cs
 * Purpose: Implements canonical vehicle access for the Vast Online backend.
 * Security Considerations: Validates identifiers, bounds result limits, parameterizes queries, and routes operations through centralized retry and rate-limiting policies.
 * Example Usage: `var vehicles = await adapter.GetByCustomerAsync(customerId, 25, cancellationToken);`
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
    /// Vast Online implementation of the <see cref="IVehicleAdapter"/> contract.
    /// </summary>
    public sealed class VehicleAdapter : SqlAdapterBase, IVehicleAdapter
    {
        private const int DefaultListLimit = 100;

        private static readonly string[] RequiredVehicleKeys =
        {
            "Vehicle.__source",
            "Vehicle.Id",
            "Vehicle.CustomerId",
            "Vehicle.Vin",
            "Vehicle.Make",
            "Vehicle.Model",
            "Vehicle.ModelYear",
            "Vehicle.Odometer"
        };

        private static readonly string[] VehicleProjectionFields =
        {
            "Id",
            "CustomerId",
            "Vin",
            "Make",
            "Model",
            "ModelYear",
            "Odometer"
        };

        private readonly int _defaultListLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="VehicleAdapter"/> class.
        /// </summary>
        /// <param name="connection">Database connection.</param>
        /// <param name="fieldMap">Field map configuration.</param>
        /// <param name="retryPolicy">Retry policy implementation.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="rateLimiter">Rate limiter controlling throughput.</param>
        /// <param name="defaultListLimit">Default maximum rows returned for list operations.</param>
        public VehicleAdapter(
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
            MappingValidator.EnsureMappings(fieldMap, RequiredVehicleKeys, nameof(VehicleAdapter));
            MappingValidator.EnsureEntitySources(fieldMap, new[] { "Vehicle" }, nameof(VehicleAdapter));
        }

        /// <inheritdoc />
        public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
            {
                throw new InvalidAdapterRequestException("Vehicle id must be provided.");
            }

            return ExecuteDbOperationAsync(
                "VehicleAdapter.GetById",
                async ct =>
                {
                    var fieldMap = FieldMap.GetTargets("Vehicle", VehicleProjectionFields);
                    var source = FieldMap.GetEntitySource("Vehicle");
                    var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
                    var commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap["Id"]} = @id";

                    await using var command = await CreateCommandAsync(commandText, ct).ConfigureAwait(false);
                    AddParameter(command, "@id", id);

                    await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        return null;
                    }

                    return ReadVehicle(reader);
                },
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<Vehicle>> GetByCustomerAsync(
            Guid customerId,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            if (customerId == Guid.Empty)
            {
                throw new InvalidAdapterRequestException("Customer id must be provided.");
            }

            var limit = EnforceLimit(maxResults, _defaultListLimit, nameof(maxResults));

            return ExecuteDbOperationAsync(
                "VehicleAdapter.GetByCustomer",
                async ct =>
                {
                    var fieldMap = FieldMap.GetTargets("Vehicle", VehicleProjectionFields);
                    var source = FieldMap.GetEntitySource("Vehicle");
                    var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
                    var commandText = $@"SELECT TOP (@limit) {selectClause}
FROM {source}
WHERE {fieldMap["CustomerId"]} = @customerId
ORDER BY {fieldMap["ModelYear"]} DESC, {fieldMap["Make"]}, {fieldMap["Model"]};";

                    await using var command = await CreateCommandAsync(commandText, ct).ConfigureAwait(false);
                    AddParameter(command, "@limit", limit, DbType.Int32);
                    AddParameter(command, "@customerId", customerId);

                    var vehicles = new List<Vehicle>();
                    await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        vehicles.Add(ReadVehicle(reader));
                    }

                    return (IReadOnlyCollection<Vehicle>)new ReadOnlyCollection<Vehicle>(vehicles);
                },
                cancellationToken);
        }

        private static Vehicle ReadVehicle(DbDataReader reader)
        {
            return new Vehicle(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Vin")),
                reader.GetString(reader.GetOrdinal("Make")),
                reader.GetString(reader.GetOrdinal("Model")),
                reader.GetInt32(reader.GetOrdinal("ModelYear")),
                reader.GetInt32(reader.GetOrdinal("Odometer")),
                reader.GetGuid(reader.GetOrdinal("CustomerId")));
        }
    }
}
