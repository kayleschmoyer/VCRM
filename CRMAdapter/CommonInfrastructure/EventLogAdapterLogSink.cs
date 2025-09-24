#nullable enable
using System;
using System.Diagnostics;
using System.Text;

namespace CRMAdapter.CommonInfrastructure;

/// <summary>
/// Writes structured adapter logs to the Windows Event Log. When running on non-Windows platforms the sink falls back
/// to stderr so diagnostics remain accessible during development.
/// </summary>
public sealed class EventLogAdapterLogSink : IAdapterLogSink
{
    private readonly string _source;
    private readonly string _logName;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventLogAdapterLogSink"/> class.
    /// </summary>
    /// <param name="source">Event source used when writing to the Windows Event Log.</param>
    /// <param name="logName">Event log name (defaults to Application).</param>
    public EventLogAdapterLogSink(string source, string? logName = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Event source must be provided.", nameof(source));
        }

        _source = source;
        _logName = string.IsNullOrWhiteSpace(logName) ? "Application" : logName!;

        if (OperatingSystem.IsWindows())
        {
            TryEnsureEventSource();
        }
    }

    /// <inheritdoc />
    public void Emit(AdapterLogRecord record)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        if (!OperatingSystem.IsWindows())
        {
            WriteToConsole(record);
            return;
        }

        try
        {
            var entryType = record.Level switch
            {
                "Error" or "error" => EventLogEntryType.Error,
                "Warning" or "warning" => EventLogEntryType.Warning,
                _ => EventLogEntryType.Information,
            };

            EventLog.WriteEntry(_source, FormatMessage(record), entryType);
        }
        catch
        {
            // Swallow logging failures; telemetry must never interfere with primary code paths.
        }
    }

    private static void WriteToConsole(AdapterLogRecord record)
    {
        var builder = new StringBuilder();
        builder.Append('[').Append(record.Timestamp.ToString("O")).Append("] ");
        builder.Append('[').Append(record.Level).Append("] ");
        builder.AppendLine(record.Message);
        if (!string.IsNullOrEmpty(record.CorrelationId))
        {
            builder.AppendLine($"CorrelationId: {record.CorrelationId}");
        }

        foreach (var kvp in record.Context)
        {
            if (string.Equals(kvp.Key, "CorrelationId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.AppendLine($"{kvp.Key}: {kvp.Value}");
        }

        if (record.Exception is not null)
        {
            builder.AppendLine(record.Exception.ToString());
        }

        Console.Error.Write(builder.ToString());
    }

    private string FormatMessage(AdapterLogRecord record)
    {
        var builder = new StringBuilder();
        builder.Append('[').Append(record.Timestamp.ToString("O")).Append("] ");
        builder.AppendLine(record.Message);
        builder.AppendLine($"Level: {record.Level}");
        if (!string.IsNullOrEmpty(record.CorrelationId))
        {
            builder.AppendLine($"CorrelationId: {record.CorrelationId}");
        }

        foreach (var kvp in record.Context)
        {
            if (string.Equals(kvp.Key, "CorrelationId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.AppendLine($"{kvp.Key}: {kvp.Value}");
        }

        if (record.Exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(record.Exception.ToString());
        }

        return builder.ToString();
    }

    private void TryEnsureEventSource()
    {
        try
        {
            if (!EventLog.SourceExists(_source))
            {
                var data = new EventSourceCreationData(_source, _logName);
                EventLog.CreateEventSource(data);
            }
        }
        catch
        {
            // Creating event sources requires administrative rights; swallow failures to avoid impacting runtime behaviour.
        }
    }
}
