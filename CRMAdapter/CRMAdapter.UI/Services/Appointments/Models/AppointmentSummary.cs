// AppointmentSummary.cs: Lightweight appointment projection for tables, calendars, and cross-module surfacing.
using System;

namespace CRMAdapter.UI.Services.Appointments.Models;

public sealed record AppointmentSummary(
    Guid Id,
    string AppointmentNumber,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    string Status,
    string Service,
    string Technician,
    AppointmentLinkedCustomer Customer,
    AppointmentLinkedVehicle Vehicle,
    string? NotesPreview);
