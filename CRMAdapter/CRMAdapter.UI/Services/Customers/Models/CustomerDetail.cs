// CustomerDetail.cs: Rich customer projection aggregating contact metadata and related collections.
using System;
using System.Collections.Generic;

namespace CRMAdapter.UI.Services.Customers.Models;

public sealed record CustomerDetail(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string? Notes,
    IReadOnlyList<VehicleRecord> Vehicles,
    IReadOnlyList<InvoiceRecord> Invoices,
    IReadOnlyList<AppointmentRecord> Appointments);
