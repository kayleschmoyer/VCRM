/*
 * File: IVehicleAdapter.cs
 * Role: Defines canonical vehicle retrieval operations shared by backend adapters.
 * Architectural Purpose: Ensures vehicle data can be accessed independently of underlying schemas.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonDomain;

namespace CRMAdapter.CommonContracts
{
    /// <summary>
    /// Contract for retrieving canonical vehicles from backends.
    /// </summary>
    public interface IVehicleAdapter
    {
        /// <summary>
        /// Gets a vehicle by its canonical identifier.
        /// </summary>
        /// <param name="id">The canonical identifier.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The vehicle when found; otherwise, <c>null</c>.</returns>
        Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves vehicles owned by a specific customer.
        /// </summary>
        /// <param name="customerId">Canonical customer identifier.</param>
        /// <param name="maxResults">Maximum results to return.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Collection of vehicles.</returns>
        Task<IReadOnlyCollection<Vehicle>> GetByCustomerAsync(
            Guid customerId,
            int maxResults,
            CancellationToken cancellationToken = default);
    }
}
