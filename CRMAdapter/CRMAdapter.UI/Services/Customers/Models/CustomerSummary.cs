// CustomerSummary.cs: Lightweight projection for customer table and card representations.
using System;

namespace CRMAdapter.UI.Services.Customers.Models;

public sealed record CustomerSummary(
    Guid Id,
    string Name,
    string Phone,
    string Email,
    int VehicleCount,
    DateTime? LastInvoiceDate);
