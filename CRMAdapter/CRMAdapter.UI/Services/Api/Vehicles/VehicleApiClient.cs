// VehicleApiClient.cs: HTTP client facade for vehicles, temporarily routing through the mock registry.
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.Storage;
using CRMAdapter.UI.Core.Sync;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Mock.Vehicles;
using CRMAdapter.UI.Services.Vehicles.Models;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Services.Api.Vehicles;

public sealed class VehicleApiClient : BaseApiClient, IVehicleService
{
    private readonly InMemoryVehicleRegistry _mock;
    private readonly ILocalCache _cache;
    private readonly ISyncQueue _syncQueue;
    private readonly OfflineSyncState _syncState;
    private readonly ILogger<VehicleApiClient> _logger;

    public VehicleApiClient(
        HttpClient client,
        InMemoryVehicleRegistry mock,
        ILocalCache cache,
        ISyncQueue syncQueue,
        OfflineSyncState syncState,
        ILogger<VehicleApiClient> logger)
        : base(client)
    {
        _mock = mock;
        _cache = cache;
        _syncQueue = syncQueue;
        _syncState = syncState;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VehicleSummary>> GetVehiclesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync("vehicles", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<VehicleSummary>();
            foreach (var vehicle in vehicles)
            {
                await _cache.SetAsync(vehicle.Id.ToString(), vehicle, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return vehicles;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached vehicles due to API failure.");
            _syncState.SetOffline(true);
            var cached = await _cache.GetAllAsync<VehicleSummary>(cancellationToken).ConfigureAwait(false);
            if (cached.Count > 0)
            {
                return cached;
            }

            var mock = await _mock.GetVehiclesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var vehicle in mock)
            {
                await _cache.SetAsync(vehicle.Id.ToString(), vehicle, cancellationToken).ConfigureAwait(false);
            }

            return mock;
        }
    }

    public async Task<VehicleDetail?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"vehicles/{vehicleId}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var detail = await response.Content.ReadFromJsonAsync<VehicleDetail>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (detail is not null)
            {
                await CacheVehicleAsync(detail, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return detail;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached vehicle {VehicleId} due to API failure.", vehicleId);
            _syncState.SetOffline(true);
            var cached = await _cache.GetAsync<VehicleDetail>(vehicleId.ToString(), cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            return await _mock.GetVehicleAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<VehicleDetail> SaveVehicleAsync(VehicleDetail vehicle, CancellationToken cancellationToken = default)
    {
        if (vehicle is null)
        {
            throw new ArgumentNullException(nameof(vehicle));
        }

        try
        {
            var response = await Client.PutAsJsonAsync($"vehicles/{vehicle.Id}", vehicle, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var updated = await response.Content.ReadFromJsonAsync<VehicleDetail>(cancellationToken: cancellationToken).ConfigureAwait(false) ?? vehicle;
            await CacheVehicleAsync(updated, cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(false);
            return updated;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Queueing vehicle update for {VehicleId} due to API failure.", vehicle.Id);
            await _syncQueue.EnqueueChangeAsync(ChangeEnvelope.ForUpdate("Vehicles", vehicle.Id.ToString(), vehicle), cancellationToken).ConfigureAwait(false);
            await CacheVehicleAsync(vehicle, cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(true);
            return vehicle;
        }
    }

    private async Task CacheVehicleAsync(VehicleDetail detail, CancellationToken cancellationToken)
    {
        await _cache.SetAsync(detail.Id.ToString(), detail, cancellationToken).ConfigureAwait(false);
        var summary = new VehicleSummary(
            detail.Id,
            detail.Vin,
            detail.Year,
            detail.Make,
            detail.Model,
            detail.Owner.Id,
            detail.Owner.Name,
            detail.Plate,
            detail.Status,
            detail.LastServiceDate);
        await _cache.SetAsync(summary.Id.ToString(), summary, cancellationToken).ConfigureAwait(false);
    }
}
