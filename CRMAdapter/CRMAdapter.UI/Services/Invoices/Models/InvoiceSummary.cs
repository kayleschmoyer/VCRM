// InvoiceSummary.cs: Lightweight projection powering tables, cards, and cross-module invoice listings.
using System;

namespace CRMAdapter.UI.Services.Invoices.Models;

public sealed record InvoiceSummary(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehicleVin,
    DateTime IssuedOn,
    string Status,
    decimal Total,
    decimal BalanceDue);
