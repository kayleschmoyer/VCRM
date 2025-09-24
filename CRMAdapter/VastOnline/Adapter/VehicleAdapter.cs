/*
 * File: VehicleAdapter.cs
 * Role: Implements canonical vehicle access for the Vast Online backend.
 * Architectural Purpose: Normalizes vehicle data retrieved from Azure SQL into the CRM canonical model.
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
        /// <param name="defaultListLimit">Default maximum rows returned for list operations.</param>
        public VehicleAdapter(DbConnection connection, FieldMap fieldMap, int defaultListLimit = DefaultListLimit)
            : base(connection, fieldMap)
        {
            _defaultListLimit = defaultListLimit;
            MappingValidator.EnsureMappings(fieldMap, RequiredVehicleKeys, nameof(VehicleAdapter));
        }

        /// <inheritdoc />
        public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fieldMap = FieldMap.GetTargets("Vehicle", VehicleProjectionFields);
            var source = FieldMap.GetEntitySource("Vehicle");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap["Id"]} = @id";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return ReadVehicle(reader);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<Vehicle>> GetByCustomerAsync(
            Guid customerId,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            var limit = Math.Min(_defaultListLimit, maxResults > 0 ? maxResults : _defaultListLimit);
            var fieldMap = FieldMap.GetTargets("Vehicle", VehicleProjectionFields);
            var source = FieldMap.GetEntitySource("Vehicle");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var commandText = $@"SELECT TOP (@limit) {selectClause}
FROM {source}
WHERE {fieldMap["CustomerId"]} = @customerId
ORDER BY {fieldMap["ModelYear"]} DESC, {fieldMap["Make"]}, {fieldMap["Model"]};";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@limit", limit, DbType.Int32);
            AddParameter(command, "@customerId", customerId);

            var vehicles = new List<Vehicle>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                vehicles.Add(ReadVehicle(reader));
            }

            return new ReadOnlyCollection<Vehicle>(vehicles);
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
