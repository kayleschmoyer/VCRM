// VehicleRecord.cs: Represents a customer's vehicle asset for tabular display in the detail view.
using System;

namespace CRMAdapter.UI.Services.Customers.Models;

public sealed record VehicleRecord(
    Guid Id,
    string Vin,
    string Year,
    string MakeModel,
    string Status);
