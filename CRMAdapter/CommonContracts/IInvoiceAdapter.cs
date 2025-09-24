/*
 * File: IInvoiceAdapter.cs
 * Role: Establishes canonical operations for invoice retrieval independent of schema specifics.
 * Architectural Purpose: Enables billing pipelines to operate uniformly across CRM backends.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonDomain;

namespace CRMAdapter.CommonContracts
{
    /// <summary>
    /// Contract for retrieving canonical invoices from backends.
    /// </summary>
    public interface IInvoiceAdapter
    {
        /// <summary>
        /// Gets an invoice by canonical identifier.
        /// </summary>
        /// <param name="id">Canonical invoice identifier.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The invoice when found; otherwise, <c>null</c>.</returns>
        Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets invoices for a given customer.
        /// </summary>
        /// <param name="customerId">Canonical customer identifier.</param>
        /// <param name="maxResults">Maximum results to return.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Invoices associated with the customer.</returns>
        Task<IReadOnlyCollection<Invoice>> GetByCustomerAsync(
            Guid customerId,
            int maxResults,
            CancellationToken cancellationToken = default);
    }
}
