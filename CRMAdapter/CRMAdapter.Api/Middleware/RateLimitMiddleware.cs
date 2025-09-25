// File: RateLimitMiddleware.cs
// Summary: Applies per-user, per-IP, and per-tenant rate limiting with structured telemetry and metrics.
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CRMAdapter.Api.Configuration;
using CRMAdapter.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMAdapter.Api.Middleware;

/// <summary>
/// Intercepts incoming HTTP requests to enforce configurable rate limits.
/// </summary>
public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestCounterService _counterService;
    private readonly IOptionsMonitor<RateLimitSettings> _settingsMonitor;
    private readonly ILogger<RateLimitMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate.</param>
    /// <param name="counterService">Service that evaluates and tracks rate limit counters.</param>
    /// <param name="settingsMonitor">Configuration monitor for rate limit settings.</param>
    /// <param name="logger">Application logger.</param>
    public RateLimitMiddleware(
        RequestDelegate next,
        RequestCounterService counterService,
        IOptionsMonitor<RateLimitSettings> settingsMonitor,
        ILogger<RateLimitMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _counterService = counterService ?? throw new ArgumentNullException(nameof(counterService));
        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the middleware logic for the current HTTP request.
    /// </summary>
    /// <param name="context">The active <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var settings = _settingsMonitor.CurrentValue;
        var normalizedPath = RateLimitSettings.NormalizePath(context.Request.Path);
        var requestContext = new RateLimitRequestContext
        {
            Method = context.Request.Method ?? HttpMethods.Get,
            OriginalPath = context.Request.Path.HasValue ? context.Request.Path.Value! : "/",
            NormalizedPath = normalizedPath,
            IsAuthenticated = context.User?.Identity?.IsAuthenticated == true,
            UserId = ResolveUserId(context.User),
            IpAddress = ResolveIpAddress(context),
            TenantId = ResolveTenantId(context, settings),
        };

        var decision = _counterService.Evaluate(requestContext, settings);
        if (decision.IsAllowed)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        await HandleThrottledRequestAsync(context, requestContext, decision, settings).ConfigureAwait(false);
    }

    private async Task HandleThrottledRequestAsync(
        HttpContext context,
        RateLimitRequestContext requestContext,
        RateLimitDecision decision,
        RateLimitSettings settings)
    {
        var correlationId = ResolveCorrelationId(context);
        var retryAfterSeconds = decision.RetryAfter?.TotalSeconds ?? settings.WindowSeconds;
        if (retryAfterSeconds < 1)
        {
            retryAfterSeconds = 1;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = Math.Ceiling(retryAfterSeconds).ToString();
        context.Response.ContentType = "application/json";

        var scopeDescription = decision.Scope switch
        {
            RateLimitScopes.User => "per-user limit",
            RateLimitScopes.Ip => "per-IP limit",
            RateLimitScopes.Tenant => "per-tenant limit",
            _ => "rate limit",
        };

        var payload = new
        {
            error = "rate_limit_exceeded",
            message = $"Too many requests: {scopeDescription} of {decision.Limit} within {settings.WindowSeconds} seconds.",
            scope = decision.Scope,
            limit = decision.Limit,
            retryAfterSeconds = Math.Ceiling(retryAfterSeconds),
            correlationId,
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, payload, cancellationToken: context.RequestAborted).ConfigureAwait(false);

        _logger.LogWarning(
            "Rate limit triggered {@ThrottleEvent}",
            new
            {
                requestContext.UserId,
                requestContext.TenantId,
                requestContext.IpAddress,
                requestContext.Method,
                Path = requestContext.OriginalPath,
                decision.Scope,
                decision.Limit,
                decision.CurrentCount,
                CorrelationId = correlationId,
            });
    }

    private static string? ResolveUserId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? ResolveClaim(params string[] types)
        {
            foreach (var type in types)
            {
                var value = user.FindFirst(type)?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        return ResolveClaim(ClaimTypes.NameIdentifier, "sub", ClaimTypes.Upn, ClaimTypes.Email) ?? user.Identity?.Name;
    }

    private static string? ResolveTenantId(HttpContext context, RateLimitSettings settings)
    {
        if (!settings.Tenant.Enabled)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(settings.Tenant.HeaderName)
            && context.Request.Headers.TryGetValue(settings.Tenant.HeaderName, out var headerValues))
        {
            var headerTenant = headerValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerTenant))
            {
                return headerTenant;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.Tenant.ClaimType))
        {
            var claimValue = context.User.FindFirst(settings.Tenant.ClaimType)?.Value;
            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                return claimValue;
            }
        }

        return null;
    }

    private static string? ResolveIpAddress(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var first = forwarded.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationHeaderName, out var existing)
            && existing is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return context.TraceIdentifier;
    }
}
