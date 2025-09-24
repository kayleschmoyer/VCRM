// File: CorrelationIdMiddleware.cs
// Summary: Ensures each request has a correlation identifier for logging and telemetry enrichment.
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace CRMAdapter.Api.Middleware;

/// <summary>
/// Adds and propagates correlation identifiers for incoming HTTP requests.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>
    /// Header used to propagate correlation identifiers.
    /// </summary>
    public const string CorrelationHeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="next">Delegate representing the next middleware in the pipeline.</param>
    /// <param name="logger">Logger used to record diagnostic messages.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the request to ensure a correlation identifier is present.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Items[CorrelationHeaderName] = correlationId;
        context.Response.Headers[CorrelationHeaderName] = correlationId;

        var activity = Activity.Current ?? new Activity("CRMAdapter.Api.Request");
        if (activity.IdFormat == ActivityIdFormat.Unknown)
        {
            activity.SetIdFormat(ActivityIdFormat.W3C);
        }

        var startedActivity = false;
        if (!activity.IsRunning)
        {
            activity.Start();
            startedActivity = true;
        }

        activity.SetTag("crm.correlation_id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceIdentifier", correlationId))
        {
            _logger.LogDebug("Correlation identifier assigned: {CorrelationId}.", correlationId);
            await _next(context).ConfigureAwait(false);
        }

        if (startedActivity)
        {
            activity.Stop();
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeaderName, out var headerValues))
        {
            var candidate = headerValues.ToString().Trim();
            if (Guid.TryParse(candidate, out var parsed))
            {
                return parsed.ToString("N");
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
