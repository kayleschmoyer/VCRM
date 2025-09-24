// InvoiceLineItem.cs: Represents a billed part or service entry on an invoice.
using System;

namespace CRMAdapter.UI.Services.Invoices.Models;

public sealed record InvoiceLineItem(
    string Description,
    string Category,
    int Quantity,
    decimal UnitPrice)
{
    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2);
}
