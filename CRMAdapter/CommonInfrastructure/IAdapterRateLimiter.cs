/*
 * File: IAdapterRateLimiter.cs
 * Purpose: Declares abstractions for per-adapter throttling to prevent backend abuse.
 * Security Considerations: Enforces operational limits to mitigate denial-of-service and credential stuffing attacks.
 * Example Usage: `using var lease = await rateLimiter.AcquireAsync("CustomerAdapter.GetById", token);`
 */
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.CommonInfrastructure
{
    /// <summary>
    /// Provides throttling primitives for adapters to prevent backend abuse.
    /// </summary>
    public interface IAdapterRateLimiter
    {
        /// <summary>
        /// Acquires a rate limiting lease for the supplied resource.
        /// </summary>
        /// <param name="resourceKey">Logical operation name (sanitized).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A disposable lease to release the slot.</returns>
        Task<IDisposable> AcquireAsync(string resourceKey, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Provides a zero-impact rate limiter used when no throttling is required.
    /// </summary>
    public sealed class NoopAdapterRateLimiter : IAdapterRateLimiter
    {
        private sealed class EmptyLease : IDisposable
        {
            public static EmptyLease Instance { get; } = new();
            private EmptyLease()
            {
            }
            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static NoopAdapterRateLimiter Instance { get; } = new();

        private NoopAdapterRateLimiter()
        {
        }

        /// <inheritdoc />
        public Task<IDisposable> AcquireAsync(string resourceKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                throw new ArgumentException("Resource key must be supplied.", nameof(resourceKey));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IDisposable>(EmptyLease.Instance);
        }
    }
}
