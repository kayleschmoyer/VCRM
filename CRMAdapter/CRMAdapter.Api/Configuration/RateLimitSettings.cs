// File: RateLimitSettings.cs
// Summary: Strongly-typed configuration model describing API rate limiting behavior and overrides.
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace CRMAdapter.Api.Configuration;

/// <summary>
/// Describes the rate limiting policy enforced by <see cref="Middleware.RateLimitMiddleware"/>.
/// </summary>
public sealed class RateLimitSettings
{
    private int _windowSeconds = 60;
    private RateLimitRule _authenticatedUser = new() { RequestsPerMinute = 100 };
    private RateLimitRule _unauthenticatedIp = new() { RequestsPerMinute = 20 };
    private Dictionary<string, EndpointRateLimit> _endpointOverrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the size of the sliding time window, in seconds, used when counting requests.
    /// </summary>
    public int WindowSeconds
    {
        get => _windowSeconds;
        set => _windowSeconds = value <= 0 ? 60 : value;
    }

    /// <summary>
    /// Gets the sliding window duration.
    /// </summary>
    public TimeSpan SlidingWindow => TimeSpan.FromSeconds(WindowSeconds);

    /// <summary>
    /// Gets or sets the default per-user limit for authenticated callers.
    /// </summary>
    public RateLimitRule AuthenticatedUser
    {
        get => _authenticatedUser;
        set => _authenticatedUser = value ?? new RateLimitRule { RequestsPerMinute = 100 };
    }

    /// <summary>
    /// Gets or sets the default per-IP limit for unauthenticated callers.
    /// </summary>
    public RateLimitRule UnauthenticatedIp
    {
        get => _unauthenticatedIp;
        set => _unauthenticatedIp = value ?? new RateLimitRule { RequestsPerMinute = 20 };
    }

    /// <summary>
    /// Gets or sets endpoint-specific overrides.
    /// </summary>
    public Dictionary<string, EndpointRateLimit> EndpointOverrides
    {
        get => _endpointOverrides;
        set => _endpointOverrides = value is null
            ? new Dictionary<string, EndpointRateLimit>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, EndpointRateLimit>(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets or sets tenant-specific throttling options.
    /// </summary>
    public TenantRateLimitSettings Tenant { get; set; } = new();

    /// <summary>
    /// Normalizes a request path to the canonical form used for lookups and metrics.
    /// </summary>
    /// <param name="path">The request path to normalize.</param>
    /// <returns>The normalized path beginning with a leading slash and without a trailing slash (unless root).</returns>
    public static string NormalizePath(PathString path)
    {
        return NormalizePath(path.HasValue ? path.Value : string.Empty);
    }

    /// <summary>
    /// Normalizes a request path to the canonical form used for lookups and metrics.
    /// </summary>
    /// <param name="path">The request path to normalize.</param>
    /// <returns>The normalized path beginning with a leading slash and without a trailing slash (unless root).</returns>
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var trimmed = path.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = $"/{trimmed}";
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Resolves the effective request-per-minute limit for an authenticated caller hitting the specified endpoint.
    /// </summary>
    /// <param name="path">The request path to examine.</param>
    /// <returns>The configured limit in requests per minute.</returns>
    public int GetAuthenticatedLimit(string path)
    {
        var normalized = NormalizePath(path);
        if (EndpointOverrides.TryGetValue(normalized, out var endpoint) && endpoint?.AuthenticatedRequestsPerMinute.HasValue == true)
        {
            return EnsurePositive(endpoint.AuthenticatedRequestsPerMinute.Value);
        }

        return EnsurePositive(AuthenticatedUser.RequestsPerMinute);
    }

    /// <summary>
    /// Resolves the effective request-per-minute limit for an unauthenticated caller hitting the specified endpoint.
    /// </summary>
    /// <param name="path">The request path to examine.</param>
    /// <returns>The configured limit in requests per minute.</returns>
    public int GetUnauthenticatedLimit(string path)
    {
        var normalized = NormalizePath(path);
        if (EndpointOverrides.TryGetValue(normalized, out var endpoint) && endpoint?.UnauthenticatedRequestsPerMinute.HasValue == true)
        {
            return EnsurePositive(endpoint.UnauthenticatedRequestsPerMinute.Value);
        }

        return EnsurePositive(UnauthenticatedIp.RequestsPerMinute);
    }

    /// <summary>
    /// Section name used for configuration binding.
    /// </summary>
    public const string SectionName = "RateLimitSettings";

    private static int EnsurePositive(int value) => value <= 0 ? 1 : value;
}

/// <summary>
/// Represents the default rate limit to apply to a caller category.
/// </summary>
public sealed class RateLimitRule
{
    private int _requestsPerMinute;

    /// <summary>
    /// Gets or sets the maximum number of requests permitted per minute.
    /// </summary>
    public int RequestsPerMinute
    {
        get => _requestsPerMinute <= 0 ? 1 : _requestsPerMinute;
        set => _requestsPerMinute = value;
    }
}

/// <summary>
/// Allows endpoint-specific overrides for authenticated and unauthenticated callers.
/// </summary>
public sealed class EndpointRateLimit
{
    /// <summary>
    /// Gets or sets the per-minute limit for authenticated callers.
    /// </summary>
    public int? AuthenticatedRequestsPerMinute { get; set; }

    /// <summary>
    /// Gets or sets the per-minute limit for unauthenticated callers.
    /// </summary>
    public int? UnauthenticatedRequestsPerMinute { get; set; }
}

/// <summary>
/// Captures tenant-aware throttling requirements.
/// </summary>
public sealed class TenantRateLimitSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether tenant-level throttling is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of requests permitted per tenant within the sliding window.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 80;

    /// <summary>
    /// Gets or sets the request header used to infer the tenant identifier.
    /// </summary>
    public string HeaderName { get; set; } = "X-Tenant-ID";

    /// <summary>
    /// Gets or sets the claim type used to infer the tenant identifier for authenticated callers.
    /// </summary>
    public string? ClaimType { get; set; } = "tenant_id";
}

/// <summary>
/// Defines the scopes returned when a rate limit decision is evaluated.
/// </summary>
public static class RateLimitScopes
{
    /// <summary>
    /// Constant identifying the per-user throttle scope.
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// Constant identifying the per-IP throttle scope.
    /// </summary>
    public const string Ip = "ip";

    /// <summary>
    /// Constant identifying the per-tenant throttle scope.
    /// </summary>
    public const string Tenant = "tenant";
}
