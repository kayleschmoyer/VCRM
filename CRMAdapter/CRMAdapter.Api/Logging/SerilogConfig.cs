// File: SerilogConfig.cs
// Summary: Configures Serilog sinks and enrichers for the API host.
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.Sinks.ApplicationInsights.TelemetryConverters;

namespace CRMAdapter.Api.Logging;

/// <summary>
/// Provides helper methods for wiring Serilog into the web application host.
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    /// Configures Serilog using environment-aware sinks.
    /// </summary>
    /// <param name="builder">Web application builder.</param>
    public static void Configure(WebApplicationBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            ConfigureLogger(context.Configuration, loggerConfiguration);
        });
    }

    private static void ConfigureLogger(
        IConfiguration configuration,
        LoggerConfiguration loggerConfiguration)
    {
        var backend = configuration["CRM:Backend"]
            ?? Environment.GetEnvironmentVariable("CRM_BACKEND")
            ?? "VAST_DESKTOP";

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .WriteTo.Console();

        if (string.Equals(backend, "VAST_DESKTOP", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureDesktopSink(configuration, loggerConfiguration);
        }
        else
        {
            ConfigureOnlineSink(configuration, loggerConfiguration);
        }
    }

    private static void ConfigureDesktopSink(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
    {
        if (!OperatingSystem.IsWindows())
        {
            loggerConfiguration.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning);
            return;
        }

        var source = configuration["Logging:EventLog:Source"]
            ?? Environment.GetEnvironmentVariable("CRM_EVENT_SOURCE")
            ?? "CRMAdapter.Api";
        var logName = configuration["Logging:EventLog:LogName"]
            ?? Environment.GetEnvironmentVariable("CRM_EVENT_LOG")
            ?? "Application";

        loggerConfiguration.WriteTo.EventLog(
            source,
            manageEventSource: true,
            eventLogName: logName,
            restrictedToMinimumLevel: LogEventLevel.Information);
    }

    private static void ConfigureOnlineSink(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
    {
        var connectionString = configuration["Logging:ApplicationInsights:ConnectionString"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            loggerConfiguration.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning);
            return;
        }

        var telemetryConverter = TelemetryConverter.Traces;
        loggerConfiguration.WriteTo.ApplicationInsights(connectionString, telemetryConverter, LogEventLevel.Information);
    }
}
