#nullable enable
using System;
using System.Globalization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace CRMAdapter.CommonInfrastructure;

/// <summary>
/// Emits structured adapter logs to Azure Application Insights.
/// </summary>
public sealed class ApplicationInsightsAdapterLogSink : IAdapterLogSink, IDisposable
{
    private readonly TelemetryClient _telemetryClient;
    private readonly string? _roleName;
    private readonly TelemetryConfiguration? _ownedConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationInsightsAdapterLogSink"/> class using a connection string.
    /// </summary>
    /// <param name="connectionString">Application Insights connection string or instrumentation key.</param>
    /// <param name="roleName">Optional cloud role name stamped on telemetry.</param>
    public ApplicationInsightsAdapterLogSink(string connectionString, string? roleName = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }

        _ownedConfiguration = TelemetryConfiguration.CreateDefault();
        _ownedConfiguration.ConnectionString = connectionString;
        _telemetryClient = new TelemetryClient(_ownedConfiguration);
        _roleName = roleName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationInsightsAdapterLogSink"/> class using an existing telemetry client.
    /// </summary>
    /// <param name="telemetryClient">Telemetry client to emit logs with.</param>
    /// <param name="roleName">Optional cloud role name stamped on telemetry.</param>
    public ApplicationInsightsAdapterLogSink(TelemetryClient telemetryClient, string? roleName = null)
    {
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _roleName = roleName;
    }

    /// <inheritdoc />
    public void Emit(AdapterLogRecord record)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var severity = record.Level switch
        {
            "Error" or "error" => SeverityLevel.Error,
            "Warning" or "warning" => SeverityLevel.Warning,
            "Debug" or "debug" => SeverityLevel.Verbose,
            _ => SeverityLevel.Information,
        };

        var trace = new TraceTelemetry(record.Message, severity)
        {
            Timestamp = record.Timestamp,
        };

        PopulateTelemetryContext(trace.Context, record);
        foreach (var kvp in record.Context)
        {
            if (kvp.Value is not null)
            {
                trace.Properties[kvp.Key] = Convert.ToString(kvp.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        if (record.Exception is not null)
        {
            trace.Properties["ExceptionType"] = record.Exception.GetType().FullName ?? "UnknownException";
        }

        _telemetryClient.TrackTrace(trace);

        if (record.Exception is not null)
        {
            var exceptionTelemetry = new ExceptionTelemetry(record.Exception)
            {
                Timestamp = record.Timestamp,
                SeverityLevel = severity,
            };

            PopulateTelemetryContext(exceptionTelemetry.Context, record);
            foreach (var kvp in record.Context)
            {
                if (kvp.Value is not null)
                {
                    exceptionTelemetry.Properties[kvp.Key] = Convert.ToString(kvp.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }

            _telemetryClient.TrackException(exceptionTelemetry);
        }
    }

    private void PopulateTelemetryContext(TelemetryContext context, AdapterLogRecord record)
    {
        if (!string.IsNullOrEmpty(record.CorrelationId))
        {
            context.Operation.Id = record.CorrelationId;
            context.Operation.ParentId = record.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(_roleName))
        {
            context.Cloud.RoleName = _roleName;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _telemetryClient.Flush();
        _ownedConfiguration?.Dispose();
    }
}
