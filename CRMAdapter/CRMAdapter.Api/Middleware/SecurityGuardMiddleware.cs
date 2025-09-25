// SecurityGuardMiddleware.cs: Blocks requests if critical secrets are unavailable.
using System;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.Api.Middleware;

public sealed class SecurityGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ResolvedSecrets _resolvedSecrets;
    private readonly ILogger<SecurityGuardMiddleware> _logger;

    public SecurityGuardMiddleware(RequestDelegate next, ResolvedSecrets resolvedSecrets, ILogger<SecurityGuardMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _resolvedSecrets = resolvedSecrets ?? throw new ArgumentNullException(nameof(resolvedSecrets));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!_resolvedSecrets.IsHealthy)
        {
            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationHeaderName, out var value) ? value?.ToString() : string.Empty;
            _logger.LogError("Critical secrets unavailable. Rejecting request with correlation {CorrelationId}.", correlationId);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service unavailable",
                Detail = "Critical secrets are unavailable. Please retry once the service recovers.",
            };
            problem.Extensions["correlationId"] = correlationId;
            await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }
}
