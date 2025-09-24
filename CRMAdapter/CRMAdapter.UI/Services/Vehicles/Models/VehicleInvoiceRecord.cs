// VehicleInvoiceRecord.cs: Captures invoicing history for a vehicle in detail timelines.
using System;

namespace CRMAdapter.UI.Services.Vehicles.Models;

public sealed record VehicleInvoiceRecord(
    string InvoiceNumber,
    DateTime IssuedOn,
    decimal Amount,
    string Status);
