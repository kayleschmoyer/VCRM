// VehicleApiClient.cs: HTTP client facade for vehicles, temporarily routing through the mock registry.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Mock.Vehicles;
using CRMAdapter.UI.Services.Vehicles.Models;

namespace CRMAdapter.UI.Services.Api.Vehicles;

public sealed class VehicleApiClient : BaseApiClient, IVehicleService
{
    private readonly InMemoryVehicleRegistry _mock;

    public VehicleApiClient(HttpClient client, InMemoryVehicleRegistry mock)
        : base(client)
    {
        _mock = mock;
    }

    public async Task<IReadOnlyList<VehicleSummary>> GetVehiclesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/vehicles once backend is wired.
        return await _mock.GetVehiclesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VehicleDetail?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/vehicles/{vehicleId} once backend is wired.
        return await _mock.GetVehicleAsync(vehicleId, cancellationToken).ConfigureAwait(false);
    }
}
