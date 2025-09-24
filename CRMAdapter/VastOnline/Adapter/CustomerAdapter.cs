/*
 * File: CustomerAdapter.cs
 * Role: Implements the canonical customer adapter for the Vast Online backend.
 * Architectural Purpose: Bridges Azure SQL customer data to the schema-agnostic CRM canonical model.
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
    /// Vast Online implementation of the <see cref="ICustomerAdapter"/> contract.
    /// </summary>
    public sealed class CustomerAdapter : SqlAdapterBase, ICustomerAdapter
    {
        private const int DefaultSearchLimit = 50;
        private const int DefaultListLimit = 100;

        private static readonly string[] RequiredCustomerKeys =
        {
            "Customer.__source",
            "Customer.Id",
            "Customer.Name",
            "Customer.Email",
            "Customer.Phone",
            "Customer.AddressLine1",
            "Customer.AddressLine2",
            "Customer.City",
            "Customer.State",
            "Customer.PostalCode",
            "Customer.Country",
            "Customer.ModifiedOn"
        };

        private static readonly string[] RequiredVehicleReferenceKeys =
        {
            "Vehicle.__source",
            "Vehicle.Id",
            "Vehicle.Vin",
            "Vehicle.CustomerId"
        };

        private static readonly string[] CustomerProjectionFields =
        {
            "Id",
            "Name",
            "Email",
            "Phone",
            "AddressLine1",
            "AddressLine2",
            "City",
            "State",
            "PostalCode",
            "Country"
        };

        private readonly int _defaultSearchLimit;
        private readonly int _defaultListLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomerAdapter"/> class.
        /// </summary>
        /// <param name="connection">Database connection.</param>
        /// <param name="fieldMap">Schema mapping definition.</param>
        /// <param name="defaultSearchLimit">Optional default maximum search results.</param>
        /// <param name="defaultListLimit">Optional default maximum list results.</param>
        public CustomerAdapter(
            DbConnection connection,
            FieldMap fieldMap,
            int defaultSearchLimit = DefaultSearchLimit,
            int defaultListLimit = DefaultListLimit)
            : base(connection, fieldMap)
        {
            _defaultSearchLimit = defaultSearchLimit;
            _defaultListLimit = defaultListLimit;
            MappingValidator.EnsureMappings(fieldMap, RequiredCustomerKeys, nameof(CustomerAdapter));
            MappingValidator.EnsureMappings(fieldMap, RequiredVehicleReferenceKeys, nameof(CustomerAdapter));
        }

        /// <inheritdoc />
        public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fieldMap = FieldMap.GetTargets("Customer", CustomerProjectionFields);
            var source = FieldMap.GetEntitySource("Customer");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap["Id"]} = @id";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var snapshot = ReadCustomerSnapshot(reader);
            var vehicleLookup = await LoadVehicleReferencesAsync(new[] { snapshot.Id }, cancellationToken).ConfigureAwait(false);
            var vehicles = vehicleLookup.TryGetValue(snapshot.Id, out var list)
                ? list
                : Array.Empty<VehicleReference>();

            return snapshot.ToCustomer(vehicles);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<Customer>> SearchByNameAsync(
            string nameQuery,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(nameQuery))
            {
                throw new ArgumentException("Name query must be supplied.", nameof(nameQuery));
            }

            var limit = Math.Min(_defaultSearchLimit, maxResults > 0 ? maxResults : _defaultSearchLimit);
            var fieldMap = FieldMap.GetTargets("Customer", CustomerProjectionFields);
            var source = FieldMap.GetEntitySource("Customer");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var commandText = $@"SELECT TOP (@limit) {selectClause}
FROM {source}
WHERE {fieldMap["Name"]} LIKE @name
ORDER BY {fieldMap["Name"]};";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@limit", limit, DbType.Int32);
            AddParameter(command, "@name", $"%{nameQuery}%");

            var snapshots = await ReadSnapshotsAsync(command, cancellationToken).ConfigureAwait(false);
            var vehicleLookup = await LoadVehicleReferencesAsync(snapshots.Select(s => s.Id), cancellationToken)
                .ConfigureAwait(false);

            return new ReadOnlyCollection<Customer>(snapshots
                .Select(snapshot =>
                {
                    var vehicles = vehicleLookup.TryGetValue(snapshot.Id, out var list)
                        ? list
                        : Array.Empty<VehicleReference>();
                    return snapshot.ToCustomer(vehicles);
                })
                .ToList());
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<Customer>> GetRecentCustomersAsync(
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            var limit = Math.Min(_defaultListLimit, maxResults > 0 ? maxResults : _defaultListLimit);
            var fieldMap = FieldMap.GetTargets("Customer", CustomerProjectionFields);
            var source = FieldMap.GetEntitySource("Customer");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var modifiedOnField = FieldMap.GetTarget("Customer.ModifiedOn");
            var commandText = $@"SELECT TOP (@limit) {selectClause}
FROM {source}
ORDER BY {modifiedOnField} DESC;";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@limit", limit, DbType.Int32);

            var snapshots = await ReadSnapshotsAsync(command, cancellationToken).ConfigureAwait(false);
            var vehicleLookup = await LoadVehicleReferencesAsync(snapshots.Select(s => s.Id), cancellationToken)
                .ConfigureAwait(false);

            return new ReadOnlyCollection<Customer>(snapshots
                .Select(snapshot =>
                {
                    var vehicles = vehicleLookup.TryGetValue(snapshot.Id, out var list)
                        ? list
                        : Array.Empty<VehicleReference>();
                    return snapshot.ToCustomer(vehicles);
                })
                .ToList());
        }

        private async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<VehicleReference>>> LoadVehicleReferencesAsync(
            IEnumerable<Guid> customerIds,
            CancellationToken cancellationToken)
        {
            var idList = customerIds.Distinct().ToList();
            if (idList.Count == 0)
            {
                return new Dictionary<Guid, IReadOnlyCollection<VehicleReference>>();
            }

            var fields = FieldMap.GetTargets("Vehicle", new[] { "Id", "Vin", "CustomerId" });
            var source = FieldMap.GetEntitySource("Vehicle");
            var parameterNames = idList.Select((_, index) => $"@c{index}").ToArray();
            var selectClause = $"{fields["Id"]} AS [VehicleId], {fields["Vin"]} AS [Vin], {fields["CustomerId"]} AS [CustomerId]";
            var commandText = $"SELECT {selectClause} FROM {source} WHERE {fields["CustomerId"]} IN ({string.Join(", ", parameterNames)})";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < idList.Count; i++)
            {
                AddParameter(command, parameterNames[i], idList[i]);
            }

            var accumulator = new Dictionary<Guid, List<VehicleReference>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var customerId = reader.GetGuid(reader.GetOrdinal("CustomerId"));
                var vehicleId = reader.GetGuid(reader.GetOrdinal("VehicleId"));
                var vin = reader.GetString(reader.GetOrdinal("Vin"));

                if (!accumulator.TryGetValue(customerId, out var vehicles))
                {
                    vehicles = new List<VehicleReference>();
                    accumulator[customerId] = vehicles;
                }

                vehicles.Add(new VehicleReference(vehicleId, vin));
            }

            return accumulator.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyCollection<VehicleReference>)pair.Value.AsReadOnly());
        }

        private static async Task<List<CustomerSnapshot>> ReadSnapshotsAsync(
            DbCommand command,
            CancellationToken cancellationToken)
        {
            var snapshots = new List<CustomerSnapshot>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                snapshots.Add(ReadCustomerSnapshot(reader));
            }

            return snapshots;
        }

        private static CustomerSnapshot ReadCustomerSnapshot(DbDataReader reader)
        {
            return new CustomerSnapshot(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetString(reader.GetOrdinal("Phone")),
                reader.GetString(reader.GetOrdinal("AddressLine1")),
                reader.IsDBNull(reader.GetOrdinal("AddressLine2")) ? string.Empty : reader.GetString(reader.GetOrdinal("AddressLine2")),
                reader.GetString(reader.GetOrdinal("City")),
                reader.GetString(reader.GetOrdinal("State")),
                reader.GetString(reader.GetOrdinal("PostalCode")),
                reader.GetString(reader.GetOrdinal("Country")));
        }

        private sealed class CustomerSnapshot
        {
            public CustomerSnapshot(
                Guid id,
                string displayName,
                string email,
                string primaryPhone,
                string line1,
                string line2,
                string city,
                string state,
                string postalCode,
                string country)
            {
                Id = id;
                DisplayName = displayName;
                Email = email;
                PrimaryPhone = primaryPhone;
                Line1 = line1;
                Line2 = line2;
                City = city;
                State = state;
                PostalCode = postalCode;
                Country = country;
            }

            public Guid Id { get; }

            public string DisplayName { get; }

            public string Email { get; }

            public string PrimaryPhone { get; }

            public string Line1 { get; }

            public string Line2 { get; }

            public string City { get; }

            public string State { get; }

            public string PostalCode { get; }

            public string Country { get; }

            public Customer ToCustomer(IReadOnlyCollection<VehicleReference> vehicles)
            {
                var address = new PostalAddress(Line1, Line2, City, State, PostalCode, Country);
                return new Customer(Id, DisplayName, Email, PrimaryPhone, address, vehicles);
            }
        }
    }
}
