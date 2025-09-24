// DashboardSnapshot.cs: Bundles KPI metrics, chart series, and recent activity for the executive dashboard.
using System.Collections.Generic;

namespace CRMAdapter.UI.Services.Dashboard.Models;

public sealed record DashboardSnapshot(
    int TotalCustomers,
    int ActiveVehicles,
    int OutstandingInvoices,
    int UpcomingAppointments,
    IReadOnlyList<MonthlyRevenuePoint> MonthlyRevenue,
    IReadOnlyList<AppointmentStatusSlice> AppointmentStatusDistribution,
    IReadOnlyList<VehiclesServicedPoint> VehiclesServiced,
    IReadOnlyList<RecentActivityItem> RecentActivity);
