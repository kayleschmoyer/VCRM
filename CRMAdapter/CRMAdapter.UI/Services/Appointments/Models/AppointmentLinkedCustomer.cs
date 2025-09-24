// AppointmentLinkedCustomer.cs: Describes the customer associated with an appointment for quick linking and badges.
using System;

namespace CRMAdapter.UI.Services.Appointments.Models;

public sealed record AppointmentLinkedCustomer(
    Guid Id,
    string Name,
    string Email,
    string Phone);
