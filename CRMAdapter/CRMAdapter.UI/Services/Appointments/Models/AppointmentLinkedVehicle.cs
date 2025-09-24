// AppointmentLinkedVehicle.cs: Identifies the vehicle tied to an appointment with routing metadata.
using System;

namespace CRMAdapter.UI.Services.Appointments.Models;

public sealed record AppointmentLinkedVehicle(
    Guid Id,
    string Vin,
    string DisplayName,
    string Status);
