// VehicleDetail.cs: Full vehicle projection combining identity, owner, and lifecycle history data.
using System;
using System.Collections.Generic;

namespace CRMAdapter.UI.Services.Vehicles.Models;

public sealed record VehicleDetail(
    Guid Id,
    string Vin,
    int Year,
    string Make,
    string Model,
    string Plate,
    string Status,
    string Notes,
    VehicleOwner Owner,
    DateTime? LastServiceDate,
    IReadOnlyList<VehicleInvoiceRecord> Invoices,
    IReadOnlyList<VehicleAppointmentRecord> Appointments);
