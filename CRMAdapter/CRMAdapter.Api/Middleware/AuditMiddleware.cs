// AuditMiddleware.cs: Captures API activity and emits structured audit events before and after execution.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.Api.Middleware;

/// <summary>
/// Emits centralized audit events for every API invocation, including denied access attempts.
/// </summary>
public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuditLogger _auditLogger;
    private readonly ILogger<AuditMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware component.</param>
    /// <param name="auditLogger">Audit logger used to emit structured events.</param>
    /// <param name="logger">Framework logger used for operational diagnostics.</param>
    public AuditMiddleware(RequestDelegate next, AuditLogger auditLogger, ILogger<AuditMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the HTTP request, emitting audit events before and after the action executes.
    /// </summary>
    /// <param name="context">Current HTTP request context.</param>
    /// <returns>A task that completes when the request has been processed.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var cancellationToken = context.RequestAborted;
        var correlationId = ResolveCorrelationId(context);
        var userId = ResolveUserId(context.User);
        var role = ResolveRole(context.User);
        var action = ResolveAction(context.Request);
        var entityId = ResolveEntityId(context.GetRouteData());

        await _auditLogger.LogAsync(
            new AuditEvent(
                correlationId,
                userId,
                role,
                action + ".Attempt",
                entityId,
                DateTimeOffset.UtcNow,
                AuditResult.Success,
                new Dictionary<string, string>
                {
                    ["origin"] = "API",
                    ["stage"] = "Attempt",
                    ["method"] = context.Request.Method,
                }),
            cancellationToken).ConfigureAwait(false);

        try
        {
            await _next(context).ConfigureAwait(false);
            var outcome = ResolveResult(context.Response?.StatusCode ?? StatusCodes.Status500InternalServerError);
            await _auditLogger.LogAsync(
                new AuditEvent(
                    correlationId,
                    userId,
                    role,
                    action,
                    entityId,
                    DateTimeOffset.UtcNow,
                    outcome,
                    new Dictionary<string, string>
                    {
                        ["origin"] = "API",
                        ["stage"] = "Completion",
                        ["statusCode"] = (context.Response?.StatusCode ?? 0).ToString(CultureInfo.InvariantCulture),
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while executing {Action} for correlation {CorrelationId}.", action, correlationId);
            await _auditLogger.LogAsync(
                new AuditEvent(
                    correlationId,
                    userId,
                    role,
                    action,
                    entityId,
                    DateTimeOffset.UtcNow,
                    AuditResult.Failure,
                    new Dictionary<string, string>
                    {
                        ["origin"] = "API",
                        ["stage"] = "Completion",
                        ["exception"] = ex.GetType().Name,
                    }),
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationHeaderName, out var value)
            && value is string explicitId
            && !string.IsNullOrWhiteSpace(explicitId))
        {
            return explicitId.Trim();
        }

        return context.TraceIdentifier;
    }

    private static string ResolveUserId(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated == true)
        {
            var claim = principal.FindFirst("sub")
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)
                ?? principal.FindFirst("oid")
                ?? principal.FindFirst("email");
            if (claim is not null && !string.IsNullOrWhiteSpace(claim.Value))
            {
                return claim.Value;
            }
        }

        return "anonymous";
    }

    private static string ResolveRole(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated == true)
        {
            var roleClaim = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).FirstOrDefault()
                ?? principal.FindAll("role").Select(claim => claim.Value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(roleClaim))
            {
                return roleClaim;
            }
        }

        return "guest";
    }

    private static string ResolveAction(HttpRequest request)
    {
        var segments = request.Path.HasValue
            ? request.Path.Value!.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

        var resource = segments.Length > 0 ? segments[0] : "Root";
        if (resource.EndsWith("s", StringComparison.OrdinalIgnoreCase) && resource.Length > 1)
        {
            resource = resource[..^1];
        }

        resource = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(resource.Replace('-', ' ')).Replace(" ", string.Empty, StringComparison.Ordinal);
        var operationSegment = segments.Length > 1 ? segments[^1] : string.Empty;
        if (!string.IsNullOrWhiteSpace(operationSegment)
            && !operationSegment.Equals(segments[0], StringComparison.OrdinalIgnoreCase)
            && !Guid.TryParse(operationSegment, out _)
            && !long.TryParse(operationSegment, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            var operation = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(operationSegment.Replace('-', ' ')).Replace(" ", string.Empty, StringComparison.Ordinal);
            return $"{resource}.{operation}";
        }

        var action = request.Method.ToUpperInvariant() switch
        {
            "GET" => "View",
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => request.Method.ToUpperInvariant(),
        };

        return $"{resource}.{action}";
    }

    private static string? ResolveEntityId(RouteData? routeData)
    {
        if (routeData is null)
        {
            return null;
        }

        foreach (var value in routeData.Values)
        {
            if (value.Value is null)
            {
                continue;
            }

            if (value.Key.EndsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = value.Value.ToString();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (Guid.TryParse(candidate, out var guid))
                {
                    return guid.ToString("N", CultureInfo.InvariantCulture);
                }

                return MaskEntity(candidate);
            }
        }

        return null;
    }

    private static string MaskEntity(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        var head = trimmed[..2];
        var tail = trimmed[^2..];
        return string.Concat(head, new string('*', trimmed.Length - 4), tail);
    }

    private static AuditResult ResolveResult(int statusCode)
    {
        if (statusCode == StatusCodes.Status401Unauthorized || statusCode == StatusCodes.Status403Forbidden)
        {
            return AuditResult.Denied;
        }

        if (statusCode >= 200 && statusCode < 400)
        {
            return AuditResult.Success;
        }

        return AuditResult.Failure;
    }
}
