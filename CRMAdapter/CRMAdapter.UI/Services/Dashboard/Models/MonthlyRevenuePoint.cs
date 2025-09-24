// MonthlyRevenuePoint.cs: Represents invoice revenue aggregated by month for charting.

namespace CRMAdapter.UI.Services.Dashboard.Models;

public sealed record MonthlyRevenuePoint(
    string Month,
    decimal Revenue);
