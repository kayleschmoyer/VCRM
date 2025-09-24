// InvoiceDetail.cs: Rich invoice representation powering the detail workspace and dialogs.
using System;
using System.Collections.Generic;

namespace CRMAdapter.UI.Services.Invoices.Models;

public sealed record InvoiceDetail(
    Guid Id,
    string InvoiceNumber,
    DateTime IssuedOn,
    DateTime DueOn,
    string Status,
    CustomerLink Customer,
    VehicleLink Vehicle,
    IReadOnlyList<InvoiceLineItem> LineItems,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    decimal PaymentsApplied,
    decimal BalanceDue,
    IReadOnlyList<PaymentRecord> Payments);
