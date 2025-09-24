// PaymentRecord.cs: Immutable record for captured payments against an invoice.
using System;

namespace CRMAdapter.UI.Services.Invoices.Models;

public sealed record PaymentRecord(
    Guid Id,
    decimal Amount,
    string Method,
    DateTime PaidOn,
    string? Notes);
