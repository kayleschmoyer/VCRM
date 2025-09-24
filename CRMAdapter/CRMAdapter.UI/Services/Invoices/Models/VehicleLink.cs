// VehicleLink.cs: Provides quick access metadata for vehicles attached to invoices.
using System;

namespace CRMAdapter.UI.Services.Invoices.Models;

public sealed record VehicleLink(
    Guid Id,
    string Vin,
    string DisplayName);
