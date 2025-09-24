// CustomerLink.cs: Reference metadata for navigating between invoices and customer detail views.
using System;

namespace CRMAdapter.UI.Services.Invoices.Models;

public sealed record CustomerLink(
    Guid Id,
    string Name,
    string Email,
    string Phone);
