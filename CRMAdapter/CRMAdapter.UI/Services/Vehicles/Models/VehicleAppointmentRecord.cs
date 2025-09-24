// VehicleAppointmentRecord.cs: Describes scheduled appointments tied to a vehicle's lifecycle.
using System;

namespace CRMAdapter.UI.Services.Vehicles.Models;

public sealed record VehicleAppointmentRecord(
    Guid Id,
    DateTime ScheduledFor,
    string Subject,
    string Owner,
    string Status);
