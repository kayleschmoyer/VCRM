/*
 * File: ICustomerAdapter.cs
 * Role: Declares canonical operations for accessing customer data across heterogeneous backends.
 * Architectural Purpose: Enforces the adapter contract for retrieving canonical customers without leaking schema details.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonDomain;

namespace CRMAdapter.CommonContracts
{
    /// <summary>
    /// Contract for retrieving canonical customers from a backend system.
    /// </summary>
    public interface ICustomerAdapter
    {
        /// <summary>
        /// Gets a customer by the canonical identifier.
        /// </summary>
        /// <param name="id">The canonical identifier.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The customer when found; otherwise, <c>null</c>.</returns>
        Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches customers by display name using backend-specific matching semantics.
        /// </summary>
        /// <param name="nameQuery">The normalized name fragment.</param>
        /// <param name="maxResults">Max number of records to return (defaults to framework configuration).</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Collection of matching customers.</returns>
        Task<IReadOnlyCollection<Customer>> SearchByNameAsync(
            string nameQuery,
            int maxResults,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the most recently modified customers for UI dashboards.
        /// </summary>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Collection of recent customers.</returns>
        Task<IReadOnlyCollection<Customer>> GetRecentCustomersAsync(
            int maxResults,
            CancellationToken cancellationToken = default);
    }
}
