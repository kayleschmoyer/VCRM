// VehiclesServicedPoint.cs: Aggregates completed appointments per month for fleet servicing trends.

namespace CRMAdapter.UI.Services.Dashboard.Models;

public sealed record VehiclesServicedPoint(
    string Month,
    int CompletedCount);
