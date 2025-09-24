#nullable enable
using System;
using System.Collections.Generic;

namespace CRMAdapter.CommonInfrastructure;

/// <summary>
/// Factory helpers for constructing structured adapter loggers targeting platform specific sinks.
/// </summary>
public static class AdapterLoggerFactory
{
    /// <summary>
    /// Creates a structured logger that writes to the Windows Event Log (falling back to stderr on non-Windows platforms).
    /// </summary>
    /// <param name="source">Event source name (defaults to CRMAdapter).</param>
    /// <param name="logName">Event log name (defaults to Application).</param>
    /// <param name="additionalSinks">Optional additional sinks to receive log events.</param>
    /// <returns>A structured adapter logger.</returns>
    public static IAdapterLogger CreateDesktopLogger(
        string? source = null,
        string? logName = null,
        IEnumerable<IAdapterLogSink>? additionalSinks = null)
    {
        var sinks = new List<IAdapterLogSink>
        {
            new EventLogAdapterLogSink(source ?? "CRMAdapter", logName ?? "Application"),
        };

        if (additionalSinks is not null)
        {
            sinks.AddRange(additionalSinks);
        }

        return new StructuredAdapterLogger(sinks);
    }

    /// <summary>
    /// Creates a structured logger that emits telemetry to Azure Application Insights.
    /// </summary>
    /// <param name="connectionString">Application Insights connection string or instrumentation key.</param>
    /// <param name="roleName">Optional cloud role name stamped on telemetry.</param>
    /// <param name="additionalSinks">Optional additional sinks to receive log events.</param>
    /// <returns>A structured adapter logger.</returns>
    public static IAdapterLogger CreateOnlineLogger(
        string connectionString,
        string? roleName = null,
        IEnumerable<IAdapterLogSink>? additionalSinks = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }

        var sinks = new List<IAdapterLogSink>
        {
            new ApplicationInsightsAdapterLogSink(connectionString, roleName),
        };

        if (additionalSinks is not null)
        {
            sinks.AddRange(additionalSinks);
        }

        return new StructuredAdapterLogger(sinks);
    }

    /// <summary>
    /// Creates a structured logger using the supplied sinks verbatim.
    /// </summary>
    /// <param name="sinks">Sinks that should receive log records.</param>
    /// <returns>A structured adapter logger.</returns>
    public static IAdapterLogger CreateFromSinks(params IAdapterLogSink[] sinks)
    {
        if (sinks is null)
        {
            throw new ArgumentNullException(nameof(sinks));
        }

        return new StructuredAdapterLogger(sinks);
    }
}
