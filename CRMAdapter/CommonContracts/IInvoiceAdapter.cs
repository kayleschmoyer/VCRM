/*
 * File: IInvoiceAdapter.cs
 * Purpose: Establishes canonical operations for invoice retrieval independent of schema specifics.
 * Security Considerations: Enforces parameterized access patterns and validates identifiers before querying billing data.
 * Example Usage: `var invoices = await adapter.GetByCustomerAsync(customerId, 50, cancellationToken);`
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
        /// <exception cref="InvalidAdapterRequestException">Thrown when the identifier is invalid.</exception>
        Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets invoices for a given customer.
        /// </summary>
        /// <param name="customerId">Canonical customer identifier.</param>
        /// <param name="maxResults">Maximum results to return.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Invoices associated with the customer.</returns>
        /// <exception cref="InvalidAdapterRequestException">Thrown when the identifier or limits are invalid.</exception>
        Task<IReadOnlyCollection<Invoice>> GetByCustomerAsync(
            Guid customerId,
            int maxResults,
            CancellationToken cancellationToken = default);
    }
}
