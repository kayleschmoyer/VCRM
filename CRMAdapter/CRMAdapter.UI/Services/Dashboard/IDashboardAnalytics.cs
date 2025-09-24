// IDashboardAnalytics.cs: Exposes aggregated KPIs and chart-ready data for the executive dashboard.
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Dashboard.Models;

namespace CRMAdapter.UI.Services.Dashboard;

public interface IDashboardAnalytics
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
