/*
 * File: Invoice.cs
 * Purpose: Provides the canonical representation for service invoices across CRM backends.
 * Security Considerations: Guards against negative monetary values, enforces identifier validation, and clones line items to maintain immutability.
 * Example Usage: `var invoice = new Invoice(id, customerId, vehicleId, "INV-1001", DateTime.UtcNow, 199.99m, "Paid", lines);`
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Canonical invoice aggregate representing customer billing details.
    /// </summary>
    public sealed class Invoice
    {
        private const int MaxInvoiceNumberLength = 64;
        private const int MaxStatusLength = 32;

        /// <summary>
        /// Initializes a new instance of the <see cref="Invoice"/> class.
        /// </summary>
        /// <param name="id">Canonical invoice identifier.</param>
        /// <param name="customerId">Customer identifier associated with the invoice.</param>
        /// <param name="vehicleId">Vehicle identifier tied to the invoice.</param>
        /// <param name="invoiceNumber">Human readable invoice number.</param>
        /// <param name="invoiceDate">Date the invoice was issued.</param>
        /// <param name="totalAmount">Total invoice amount.</param>
        /// <param name="status">Current invoice status.</param>
        /// <param name="lineItems">Line items billed on the invoice.</param>
        public Invoice(
            Guid id,
            Guid customerId,
            Guid vehicleId,
            string invoiceNumber,
            DateTime invoiceDate,
            decimal totalAmount,
            string status,
            IReadOnlyCollection<InvoiceLine> lineItems)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Invoice id must be non-empty.", nameof(id));
            }

            if (customerId == Guid.Empty)
            {
                throw new ArgumentException("Customer id must be non-empty.", nameof(customerId));
            }

            if (vehicleId == Guid.Empty)
            {
                throw new ArgumentException("Vehicle id must be non-empty.", nameof(vehicleId));
            }

            if (totalAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalAmount), "Invoice total cannot be negative.");
            }

            Id = id;
            CustomerId = customerId;
            VehicleId = vehicleId;
            InvoiceNumber = ValidateRequired(invoiceNumber, nameof(invoiceNumber), MaxInvoiceNumberLength);
            InvoiceDate = invoiceDate;
            TotalAmount = totalAmount;
            Status = ValidateRequired(status, nameof(status), MaxStatusLength);
            LineItems = CloneLineItems(lineItems);
        }

        /// <summary>
        /// Gets the canonical invoice identifier.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the canonical customer identifier.
        /// </summary>
        public Guid CustomerId { get; }

        /// <summary>
        /// Gets the canonical vehicle identifier.
        /// </summary>
        public Guid VehicleId { get; }

        /// <summary>
        /// Gets the invoice number.
        /// </summary>
        public string InvoiceNumber { get; }

        /// <summary>
        /// Gets the invoice issue date.
        /// </summary>
        public DateTime InvoiceDate { get; }

        /// <summary>
        /// Gets the total invoice amount.
        /// </summary>
        public decimal TotalAmount { get; }

        /// <summary>
        /// Gets the canonical invoice status.
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets the immutable collection of invoice line items.
        /// </summary>
        public IReadOnlyCollection<InvoiceLine> LineItems { get; }

        private static string ValidateRequired(string value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} must be provided.", parameterName);
            }

            var trimmed = value.Trim();
            if (trimmed.Length > maxLength)
            {
                throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
            }

            return trimmed;
        }

        private static IReadOnlyCollection<InvoiceLine> CloneLineItems(IReadOnlyCollection<InvoiceLine> lineItems)
        {
            if (lineItems is null)
            {
                throw new ArgumentNullException(nameof(lineItems));
            }

            var items = lineItems.Where(item => item is not null).ToList();
            if (items.Count != lineItems.Count)
            {
                throw new ArgumentException("Line items cannot contain null entries.", nameof(lineItems));
            }

            return new ReadOnlyCollection<InvoiceLine>(items);
        }
    }

    /// <summary>
    /// Canonical invoice line item.
    /// </summary>
    public sealed class InvoiceLine
    {
        private const int MaxDescriptionLength = 256;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvoiceLine"/> class.
        /// </summary>
        /// <param name="description">Line item description.</param>
        /// <param name="quantity">Line item quantity.</param>
        /// <param name="unitPrice">Line item unit price.</param>
        /// <param name="taxAmount">Associated tax amount.</param>
        public InvoiceLine(string description, decimal quantity, decimal unitPrice, decimal taxAmount)
        {
            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
            }

            if (unitPrice < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
            }

            if (taxAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(taxAmount), "Tax amount cannot be negative.");
            }

            Description = ValidateDescription(description);
            Quantity = quantity;
            UnitPrice = unitPrice;
            TaxAmount = taxAmount;
        }

        /// <summary>
        /// Gets the line item description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the quantity applied.
        /// </summary>
        public decimal Quantity { get; }

        /// <summary>
        /// Gets the unit price.
        /// </summary>
        public decimal UnitPrice { get; }

        /// <summary>
        /// Gets the tax amount.
        /// </summary>
        public decimal TaxAmount { get; }

        private static string ValidateDescription(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Description must be provided.", nameof(value));
            }

            var trimmed = value.Trim();
            if (trimmed.Length > MaxDescriptionLength)
            {
                throw new ArgumentException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(value));
            }

            return trimmed;
        }
    }
}
