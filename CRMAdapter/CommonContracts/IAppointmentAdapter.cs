/*
 * File: IAppointmentAdapter.cs
 * Purpose: Declares canonical appointment operations for backend adapters.
 * Security Considerations: Enforces sanitized date-range queries and supports throttling through the rate limiting abstraction.
 * Example Usage: `var appointments = await adapter.GetByDateAsync(DateTime.UtcNow.Date, 100, cancellationToken);`
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonDomain;

namespace CRMAdapter.CommonContracts
{
    /// <summary>
    /// Contract for retrieving canonical appointments from backends.
    /// </summary>
    public interface IAppointmentAdapter
    {
        /// <summary>
        /// Gets an appointment by canonical identifier.
        /// </summary>
        /// <param name="id">Canonical appointment identifier.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The appointment when found; otherwise, <c>null</c>.</returns>
        /// <exception cref="InvalidAdapterRequestException">Thrown when the identifier is invalid.</exception>
        Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves appointments scheduled for a specific day.
        /// </summary>
        /// <param name="date">Date for which appointments are retrieved.</param>
        /// <param name="maxResults">Maximum results to return.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Collection of appointments.</returns>
        /// <exception cref="InvalidAdapterRequestException">Thrown when the request is invalid.</exception>
        Task<IReadOnlyCollection<Appointment>> GetByDateAsync(
            DateTime date,
            int maxResults,
            CancellationToken cancellationToken = default);
    }
}
