// IVehicleService.cs: Stable contract for vehicle catalog and detail retrieval across data providers.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Vehicles.Models;

namespace CRMAdapter.UI.Services.Contracts;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleSummary>> GetVehiclesAsync(CancellationToken cancellationToken = default);

    Task<VehicleDetail?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<VehicleDetail> SaveVehicleAsync(VehicleDetail vehicle, CancellationToken cancellationToken = default);
}
