/*
 * File: InvoiceAdapter.cs
 * Role: Implements canonical invoice access for the Vast Online backend.
 * Architectural Purpose: Converts Azure SQL invoice data into the CRM canonical invoice aggregate with line items.
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
    /// Vast Online implementation of the <see cref="IInvoiceAdapter"/> contract.
    /// </summary>
    public sealed class InvoiceAdapter : SqlAdapterBase, IInvoiceAdapter
    {
        private const int DefaultListLimit = 100;

        private static readonly string[] RequiredInvoiceKeys =
        {
            "Invoice.__source",
            "Invoice.Id",
            "Invoice.CustomerId",
            "Invoice.VehicleId",
            "Invoice.Number",
            "Invoice.Date",
            "Invoice.Total",
            "Invoice.Status"
        };

        private static readonly string[] RequiredInvoiceLineKeys =
        {
            "InvoiceLine.__source",
            "InvoiceLine.InvoiceId",
            "InvoiceLine.Description",
            "InvoiceLine.Quantity",
            "InvoiceLine.UnitPrice",
            "InvoiceLine.Tax"
        };

        private static readonly string[] InvoiceProjectionFields =
        {
            "Id",
            "CustomerId",
            "VehicleId",
            "Number",
            "Date",
            "Total",
            "Status"
        };

        private readonly int _defaultListLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvoiceAdapter"/> class.
        /// </summary>
        /// <param name="connection">Database connection.</param>
        /// <param name="fieldMap">Field map configuration.</param>
        /// <param name="defaultListLimit">Default maximum results for list operations.</param>
        public InvoiceAdapter(DbConnection connection, FieldMap fieldMap, int defaultListLimit = DefaultListLimit)
            : base(connection, fieldMap)
        {
            _defaultListLimit = defaultListLimit;
            MappingValidator.EnsureMappings(fieldMap, RequiredInvoiceKeys, nameof(InvoiceAdapter));
            MappingValidator.EnsureMappings(fieldMap, RequiredInvoiceLineKeys, nameof(InvoiceAdapter));
        }

        /// <inheritdoc />
        public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fieldMap = FieldMap.GetTargets("Invoice", InvoiceProjectionFields);
            var source = FieldMap.GetEntitySource("Invoice");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap["Id"]} = @id";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var invoice = ReadInvoice(reader);
            var lines = await LoadInvoiceLinesAsync(new[] { invoice.Id }, cancellationToken).ConfigureAwait(false);
            var lineItems = lines.TryGetValue(invoice.Id, out var collection)
                ? collection
                : Array.Empty<InvoiceLine>();

            return new Invoice(
                invoice.Id,
                invoice.CustomerId,
                invoice.VehicleId,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.TotalAmount,
                invoice.Status,
                lineItems);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<Invoice>> GetByCustomerAsync(
            Guid customerId,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            var limit = Math.Min(_defaultListLimit, maxResults > 0 ? maxResults : _defaultListLimit);
            var fieldMap = FieldMap.GetTargets("Invoice", InvoiceProjectionFields);
            var source = FieldMap.GetEntitySource("Invoice");
            var selectClause = string.Join(", ", fieldMap.Select(kvp => $"{kvp.Value} AS [{kvp.Key}]"));
            var commandText = $@"SELECT TOP (@limit) {selectClause}
FROM {source}
WHERE {fieldMap["CustomerId"]} = @customerId
ORDER BY {fieldMap["Date"]} DESC;";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            AddParameter(command, "@limit", limit, DbType.Int32);
            AddParameter(command, "@customerId", customerId);

            var invoices = new List<InvoiceRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                invoices.Add(ReadInvoice(reader));
            }

            var lineLookup = await LoadInvoiceLinesAsync(invoices.Select(i => i.Id), cancellationToken).ConfigureAwait(false);
            var results = invoices.Select(record =>
            {
                var lines = lineLookup.TryGetValue(record.Id, out var items)
                    ? items
                    : Array.Empty<InvoiceLine>();
                return new Invoice(
                    record.Id,
                    record.CustomerId,
                    record.VehicleId,
                    record.InvoiceNumber,
                    record.InvoiceDate,
                    record.TotalAmount,
                    record.Status,
                    lines);
            }).ToList();

            return new ReadOnlyCollection<Invoice>(results);
        }

        private async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<InvoiceLine>>> LoadInvoiceLinesAsync(
            IEnumerable<Guid> invoiceIds,
            CancellationToken cancellationToken)
        {
            var ids = invoiceIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<Guid, IReadOnlyCollection<InvoiceLine>>();
            }

            var fields = FieldMap.GetTargets("InvoiceLine", new[] { "InvoiceId", "Description", "Quantity", "UnitPrice", "Tax" });
            var source = FieldMap.GetEntitySource("InvoiceLine");
            var parameterNames = ids.Select((_, index) => $"@i{index}").ToArray();
            var selectClause = $"{fields["InvoiceId"]} AS [InvoiceId], {fields["Description"]} AS [Description], {fields["Quantity"]} AS [Quantity], {fields["UnitPrice"]} AS [UnitPrice], {fields["Tax"]} AS [Tax]";
            var commandText = $"SELECT {selectClause} FROM {source} WHERE {fields["InvoiceId"]} IN ({string.Join(", ", parameterNames)})";

            await using var command = await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < ids.Count; i++)
            {
                AddParameter(command, parameterNames[i], ids[i]);
            }

            var accumulator = new Dictionary<Guid, List<InvoiceLine>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var invoiceId = reader.GetGuid(reader.GetOrdinal("InvoiceId"));
                var description = reader.GetString(reader.GetOrdinal("Description"));
                var quantity = reader.GetDecimal(reader.GetOrdinal("Quantity"));
                var unitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice"));
                var tax = reader.GetDecimal(reader.GetOrdinal("Tax"));

                if (!accumulator.TryGetValue(invoiceId, out var list))
                {
                    list = new List<InvoiceLine>();
                    accumulator[invoiceId] = list;
                }

                list.Add(new InvoiceLine(description, quantity, unitPrice, tax));
            }

            return accumulator.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyCollection<InvoiceLine>)pair.Value.AsReadOnly());
        }

        private static InvoiceRecord ReadInvoice(DbDataReader reader)
        {
            return new InvoiceRecord(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetGuid(reader.GetOrdinal("CustomerId")),
                reader.GetGuid(reader.GetOrdinal("VehicleId")),
                reader.GetString(reader.GetOrdinal("Number")),
                reader.GetDateTime(reader.GetOrdinal("Date")),
                reader.GetDecimal(reader.GetOrdinal("Total")),
                reader.GetString(reader.GetOrdinal("Status")));
        }

        private sealed class InvoiceRecord
        {
            public InvoiceRecord(
                Guid id,
                Guid customerId,
                Guid vehicleId,
                string invoiceNumber,
                DateTime invoiceDate,
                decimal totalAmount,
                string status)
            {
                Id = id;
                CustomerId = customerId;
                VehicleId = vehicleId;
                InvoiceNumber = invoiceNumber;
                InvoiceDate = invoiceDate;
                TotalAmount = totalAmount;
                Status = status;
            }

            public Guid Id { get; }

            public Guid CustomerId { get; }

            public Guid VehicleId { get; }

            public string InvoiceNumber { get; }

            public DateTime InvoiceDate { get; }

            public decimal TotalAmount { get; }

            public string Status { get; }
        }
    }
}
