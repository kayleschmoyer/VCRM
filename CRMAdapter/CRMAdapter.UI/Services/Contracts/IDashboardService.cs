// IDashboardService.cs: Stable contract for retrieving dashboard KPIs regardless of backing provider.
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Dashboard.Models;

namespace CRMAdapter.UI.Services.Contracts;

public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
