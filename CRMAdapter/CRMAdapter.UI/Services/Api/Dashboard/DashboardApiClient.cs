// DashboardApiClient.cs: HTTP client facade for dashboard KPIs, currently sourcing data from the mock analytics service.
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Dashboard.Models;
using CRMAdapter.UI.Services.Mock.Dashboard;

namespace CRMAdapter.UI.Services.Api.Dashboard;

public sealed class DashboardApiClient : BaseApiClient, IDashboardService
{
    private readonly InMemoryDashboardAnalytics _mock;

    public DashboardApiClient(HttpClient client, InMemoryDashboardAnalytics mock)
        : base(client)
    {
        _mock = mock;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/dashboard/snapshot once backend is wired.
        return await _mock.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }
}
