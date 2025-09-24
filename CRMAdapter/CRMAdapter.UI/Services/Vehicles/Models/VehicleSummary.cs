// VehicleSummary.cs: Lightweight projection for vehicle list views with owner and status context.
using System;

namespace CRMAdapter.UI.Services.Vehicles.Models;

public sealed record VehicleSummary(
    Guid Id,
    string Vin,
    int Year,
    string Make,
    string Model,
    Guid CustomerId,
    string CustomerName,
    string Plate,
    string Status,
    DateTime? LastServiceDate);
