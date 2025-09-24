// PaymentEntry.cs: Submission payload for recording a new payment.
using System;

namespace CRMAdapter.UI.Services.Invoices.Models;

public sealed record PaymentEntry(
    decimal Amount,
    string Method,
    DateTime PaidOn,
    string? Notes);
