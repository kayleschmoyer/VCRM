// InvoiceRecord.cs: Captures financial metadata for customer invoice listings.
using System;

namespace CRMAdapter.UI.Services.Customers.Models;

public sealed record InvoiceRecord(
    string InvoiceNumber,
    DateTime IssuedOn,
    decimal Amount,
    string Status);
