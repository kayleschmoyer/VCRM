// VehicleOwner.cs: Represents the customer relationship for a vehicle detail view.
using System;

namespace CRMAdapter.UI.Services.Vehicles.Models;

public sealed record VehicleOwner(Guid Id, string Name, string? PrimaryContact = null);
