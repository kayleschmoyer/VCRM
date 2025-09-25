// File: RequestCounterService.cs
// Summary: Tracks per-identity sliding-window counters and surfaces rate limit decisions and metrics.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CRMAdapter.Api.Configuration;

namespace CRMAdapter.Api.Services;

/// <summary>
/// Provides sliding-window accounting for API requests along with aggregated telemetry.
/// </summary>
public sealed class RequestCounterService
{
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, SlidingWindowCounter> _limitCounters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SlidingWindowMetric> _endpointMetrics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SlidingWindowMetric> _blockedEndpointMetrics = new(StringComparer.OrdinalIgnoreCase);
    private long _blockedRequests;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestCounterService"/> class.
    /// </summary>
    /// <param name="timeProvider">Provider used to resolve timestamps. Defaults to <see cref="TimeProvider.System"/>.</param>
    public RequestCounterService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Evaluates a request against configured rate limits.
    /// </summary>
    /// <param name="context">The request metadata.</param>
    /// <param name="settings">Rate limiting configuration.</param>
    /// <returns>The resulting decision capturing whether processing may continue.</returns>
    public RateLimitDecision Evaluate(RateLimitRequestContext context, RateLimitSettings settings)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var window = settings.SlidingWindow;
        var now = _timeProvider.GetUtcNow();

        var requirements = BuildRequirements(context, settings);
        if (requirements.Count == 0)
        {
            RecordAllowed(context, window, now);
            return RateLimitDecision.Allowed;
        }

        var leases = new List<IDisposable>(requirements.Count);
        foreach (var requirement in requirements)
        {
            var counter = _limitCounters.GetOrAdd(requirement.Key, _ => new SlidingWindowCounter());
            if (!counter.TryAcquire(now, window, requirement.Limit, out var lease, out var currentCount, out var retryAfter))
            {
                foreach (var held in leases)
                {
                    held.Dispose();
                }

                RecordBlocked(context, window, now);
                return RateLimitDecision.CreateBlocked(requirement.Scope, requirement.Limit, currentCount, requirement.Key, retryAfter);
            }

            leases.Add(lease!);
        }

        leases.Clear();
        RecordAllowed(context, window, now);
        return RateLimitDecision.Allowed;
    }

    /// <summary>
    /// Produces a snapshot of the current counters suitable for exporting via telemetry endpoints.
    /// </summary>
    /// <param name="settings">Rate limiting configuration.</param>
    /// <returns>A metrics snapshot describing active and blocked requests.</returns>
    public RateLimitMetricsSnapshot GetMetricsSnapshot(RateLimitSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var now = _timeProvider.GetUtcNow();
        var window = settings.SlidingWindow;
        var endpoints = new Dictionary<string, EndpointRateLimitMetrics>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in _endpointMetrics)
        {
            var active = kvp.Value.GetCount(now, window);
            endpoints[kvp.Key] = new EndpointRateLimitMetrics(active, 0);
        }

        foreach (var kvp in _blockedEndpointMetrics)
        {
            var blocked = kvp.Value.GetCount(now, window);
            if (endpoints.TryGetValue(kvp.Key, out var existing))
            {
                endpoints[kvp.Key] = existing with { BlockedRequests = blocked };
            }
            else
            {
                endpoints[kvp.Key] = new EndpointRateLimitMetrics(0, blocked);
            }
        }

        var activeTotal = endpoints.Values.Sum(e => (long)e.ActiveRequests);
        var blockedWindowTotal = endpoints.Values.Sum(e => (long)e.BlockedRequests);

        return new RateLimitMetricsSnapshot(
            now,
            settings.WindowSeconds,
            activeTotal,
            blockedWindowTotal,
            Interlocked.Read(ref _blockedRequests),
            endpoints);
    }

    private static string ComposeUserKey(RateLimitRequestContext context)
    {
        var tenant = string.IsNullOrWhiteSpace(context.TenantId) ? "default" : context.TenantId!.Trim();
        return $"user:{tenant}:{context.UserId}";
    }

    private static string ComposeIpKey(RateLimitRequestContext context)
    {
        var ip = string.IsNullOrWhiteSpace(context.IpAddress) ? "anonymous" : context.IpAddress!;
        return $"ip:{ip}";
    }

    private static string ComposeTenantKey(RateLimitRequestContext context)
    {
        return $"tenant:{context.TenantId}";
    }

    private List<LimitRequirement> BuildRequirements(RateLimitRequestContext context, RateLimitSettings settings)
    {
        var requirements = new List<LimitRequirement>(3);

        if (context.IsAuthenticated && !string.IsNullOrWhiteSpace(context.UserId))
        {
            var limit = settings.GetAuthenticatedLimit(context.NormalizedPath);
            requirements.Add(new LimitRequirement(ComposeUserKey(context), limit, RateLimitScopes.User));
        }
        else if (!string.IsNullOrWhiteSpace(context.IpAddress))
        {
            var limit = settings.GetUnauthenticatedLimit(context.NormalizedPath);
            requirements.Add(new LimitRequirement(ComposeIpKey(context), limit, RateLimitScopes.Ip));
        }

        if (settings.Tenant.Enabled && !string.IsNullOrWhiteSpace(context.TenantId))
        {
            var limit = settings.Tenant.RequestsPerMinute;
            requirements.Add(new LimitRequirement(ComposeTenantKey(context), limit <= 0 ? 1 : limit, RateLimitScopes.Tenant));
        }

        return requirements;
    }

    private void RecordAllowed(RateLimitRequestContext context, TimeSpan window, DateTimeOffset timestamp)
    {
        var key = context.EndpointKey;
        var metric = _endpointMetrics.GetOrAdd(key, _ => new SlidingWindowMetric());
        metric.Record(timestamp, window);
    }

    private void RecordBlocked(RateLimitRequestContext context, TimeSpan window, DateTimeOffset timestamp)
    {
        Interlocked.Increment(ref _blockedRequests);
        var key = context.EndpointKey;
        var metric = _blockedEndpointMetrics.GetOrAdd(key, _ => new SlidingWindowMetric());
        metric.Record(timestamp, window);
    }

    private sealed record LimitRequirement(string Key, int Limit, string Scope);

    private sealed class SlidingWindowCounter
    {
        private readonly LinkedList<DateTimeOffset> _timestamps = new();
        private readonly object _lock = new();

        public bool TryAcquire(
            DateTimeOffset now,
            TimeSpan window,
            int limit,
            out IDisposable? lease,
            out int currentCount,
            out TimeSpan? retryAfter)
        {
            lock (_lock)
            {
                Prune(now, window);
                if (_timestamps.Count >= limit)
                {
                    lease = null;
                    currentCount = _timestamps.Count;
                    retryAfter = CalculateRetryAfter(now, window);
                    return false;
                }

                var node = _timestamps.AddLast(now);
                lease = new Lease(this, node);
                currentCount = _timestamps.Count;
                retryAfter = null;
                return true;
            }
        }

        private TimeSpan? CalculateRetryAfter(DateTimeOffset now, TimeSpan window)
        {
            if (_timestamps.First is null)
            {
                return null;
            }

            var oldest = _timestamps.First.Value;
            var elapsed = now - oldest;
            var remaining = window - elapsed;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        private void Prune(DateTimeOffset now, TimeSpan window)
        {
            while (_timestamps.First is { } node)
            {
                if (now - node.Value < window)
                {
                    break;
                }

                _timestamps.RemoveFirst();
            }
        }

        private void Release(LinkedListNode<DateTimeOffset> node)
        {
            lock (_lock)
            {
                _timestamps.Remove(node);
            }
        }

        private sealed class Lease : IDisposable
        {
            private SlidingWindowCounter? _owner;
            private LinkedListNode<DateTimeOffset>? _node;

            public Lease(SlidingWindowCounter owner, LinkedListNode<DateTimeOffset> node)
            {
                _owner = owner;
                _node = node;
            }

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner is null)
                {
                    return;
                }

                var node = Interlocked.Exchange(ref _node, null);
                if (node is not null)
                {
                    owner.Release(node);
                }
            }
        }
    }

    private sealed class SlidingWindowMetric
    {
        private readonly LinkedList<DateTimeOffset> _timestamps = new();
        private readonly object _lock = new();

        public void Record(DateTimeOffset timestamp, TimeSpan window)
        {
            lock (_lock)
            {
                Prune(timestamp, window);
                _timestamps.AddLast(timestamp);
            }
        }

        public int GetCount(DateTimeOffset timestamp, TimeSpan window)
        {
            lock (_lock)
            {
                Prune(timestamp, window);
                return _timestamps.Count;
            }
        }

        private void Prune(DateTimeOffset now, TimeSpan window)
        {
            while (_timestamps.First is { } node)
            {
                if (now - node.Value < window)
                {
                    break;
                }

                _timestamps.RemoveFirst();
            }
        }
    }
}

/// <summary>
/// Represents the request-specific context supplied to <see cref="RequestCounterService"/>.
/// </summary>
public sealed class RateLimitRequestContext
{
    /// <summary>
    /// Gets or sets the HTTP method.
    /// </summary>
    public string Method { get; init; } = "GET";

    /// <summary>
    /// Gets or sets the original path requested.
    /// </summary>
    public string OriginalPath { get; init; } = "/";

    /// <summary>
    /// Gets or sets the normalized path used for lookups.
    /// </summary>
    public string NormalizedPath { get; init; } = "/";

    /// <summary>
    /// Gets or sets a value indicating whether the caller is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets or sets the resolved user identifier.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets or sets the resolved tenant identifier.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Gets or sets the caller IP address.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Gets the endpoint key used for metrics aggregation.
    /// </summary>
    public string EndpointKey => $"{Method.ToUpperInvariant()} {NormalizedPath}";
}

/// <summary>
/// Represents the outcome of rate limit evaluation.
/// </summary>
public sealed record RateLimitDecision(bool IsAllowed, string? Scope, int? Limit, int? CurrentCount, string? Key, TimeSpan? RetryAfter)
{
    /// <summary>
    /// Gets a static instance representing an allowed decision.
    /// </summary>
    public static RateLimitDecision Allowed { get; } = new(true, null, null, null, null, null);

    /// <summary>
    /// Creates a blocked decision instance.
    /// </summary>
    /// <param name="scope">The scope that triggered the rejection.</param>
    /// <param name="limit">The configured limit.</param>
    /// <param name="currentCount">The current number of requests counted in the window.</param>
    /// <param name="key">The internal counter key.</param>
    /// <param name="retryAfter">The estimated retry-after duration.</param>
    /// <returns>A populated decision.</returns>
    public static RateLimitDecision CreateBlocked(string scope, int limit, int currentCount, string key, TimeSpan? retryAfter)
        => new(false, scope, limit, currentCount, key, retryAfter);
}

/// <summary>
/// Aggregated metrics exposed by <see cref="RequestCounterService"/>.
/// </summary>
/// <param name="Timestamp">The timestamp when the snapshot was captured.</param>
/// <param name="WindowSeconds">The sliding window size in seconds.</param>
/// <param name="ActiveRequests">The total active requests counted across endpoints.</param>
/// <param name="BlockedRequests">The total blocked requests observed during the current window.</param>
/// <param name="BlockedRequestsSinceStart">The cumulative number of blocked requests since startup.</param>
/// <param name="Endpoints">Per-endpoint counts.</param>
public sealed record RateLimitMetricsSnapshot(
    DateTimeOffset Timestamp,
    int WindowSeconds,
    long ActiveRequests,
    long BlockedRequests,
    long BlockedRequestsSinceStart,
    IReadOnlyDictionary<string, EndpointRateLimitMetrics> Endpoints);

/// <summary>
/// Represents per-endpoint rate limit activity.
/// </summary>
/// <param name="ActiveRequests">The number of requests observed in the current window.</param>
/// <param name="BlockedRequests">The number of requests blocked in the current window.</param>
public sealed record EndpointRateLimitMetrics(long ActiveRequests, long BlockedRequests);
