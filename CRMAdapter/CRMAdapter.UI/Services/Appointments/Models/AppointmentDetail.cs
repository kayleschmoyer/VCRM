// AppointmentDetail.cs: Rich appointment detail payload supplying the detail view and action surfaces.
using System;

namespace CRMAdapter.UI.Services.Appointments.Models;

public sealed record AppointmentDetail(
    Guid Id,
    string AppointmentNumber,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    string Status,
    string Service,
    string Description,
    string Technician,
    string Location,
    string Notes,
    AppointmentLinkedCustomer Customer,
    AppointmentLinkedVehicle Vehicle,
    DateTime CreatedOn,
    DateTime LastUpdatedOn);
