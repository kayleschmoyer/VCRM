/*
 * File: Invoice.cs
 * Role: Provides the canonical representation for service invoices across CRM backends.
 * Architectural Purpose: Enables consistent billing analytics and reporting by abstracting source schema differences.
 */
using System;
using System.Collections.Generic;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Canonical invoice aggregate representing customer billing details.
    /// </summary>
    public sealed class Invoice
    {
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
            Id = id;
            CustomerId = customerId;
            VehicleId = vehicleId;
            InvoiceNumber = invoiceNumber ?? throw new ArgumentNullException(nameof(invoiceNumber));
            InvoiceDate = invoiceDate;
            TotalAmount = totalAmount;
            Status = status ?? throw new ArgumentNullException(nameof(status));
            LineItems = lineItems ?? throw new ArgumentNullException(nameof(lineItems));
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
    }

    /// <summary>
    /// Canonical invoice line item.
    /// </summary>
    public sealed class InvoiceLine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvoiceLine"/> class.
        /// </summary>
        /// <param name="description">Line item description.</param>
        /// <param name="quantity">Line item quantity.</param>
        /// <param name="unitPrice">Line item unit price.</param>
        /// <param name="taxAmount">Associated tax amount.</param>
        public InvoiceLine(string description, decimal quantity, decimal unitPrice, decimal taxAmount)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
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
    }
}
