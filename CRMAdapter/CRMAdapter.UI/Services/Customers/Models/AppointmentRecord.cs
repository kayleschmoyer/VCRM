// AppointmentRecord.cs: Tracks scheduled engagements associated with a customer.
using System;

namespace CRMAdapter.UI.Services.Customers.Models;

public sealed record AppointmentRecord(
    Guid Id,
    DateTime ScheduledFor,
    string Subject,
    string Owner,
    string Status);
