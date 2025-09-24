// IVehicleRegistry.cs: Abstraction for querying vehicles and related history streams.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Vehicles.Models;

namespace CRMAdapter.UI.Services.Vehicles;

public interface IVehicleRegistry
{
    Task<IReadOnlyList<VehicleSummary>> GetVehiclesAsync(CancellationToken cancellationToken = default);

    Task<VehicleDetail?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
