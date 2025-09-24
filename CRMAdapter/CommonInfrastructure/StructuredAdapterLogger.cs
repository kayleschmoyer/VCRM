#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMAdapter.CommonInfrastructure;

/// <summary>
/// Provides structured logging that enriches entries with correlation identifiers before forwarding to configured sinks.
/// </summary>
public sealed class StructuredAdapterLogger : IAdapterLogger
{
    private readonly IReadOnlyList<IAdapterLogSink> _sinks;
    private readonly Func<DateTimeOffset> _timestampProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredAdapterLogger"/> class.
    /// </summary>
    /// <param name="sinks">Collection of log sinks that will receive log records.</param>
    /// <param name="timestampProvider">Optional timestamp provider primarily used for testing.</param>
    public StructuredAdapterLogger(IEnumerable<IAdapterLogSink> sinks, Func<DateTimeOffset>? timestampProvider = null)
    {
        if (sinks is null)
        {
            throw new ArgumentNullException(nameof(sinks));
        }

        var sinkList = sinks.ToList();
        if (sinkList.Count == 0)
        {
            throw new ArgumentException("At least one sink must be provided.", nameof(sinks));
        }

        _sinks = sinkList;
        _timestampProvider = timestampProvider ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public void LogDebug(string message, IReadOnlyDictionary<string, object?>? context = null)
        => Write("Debug", message, null, context);

    /// <inheritdoc />
    public void LogInformation(string message, IReadOnlyDictionary<string, object?>? context = null)
        => Write("Information", message, null, context);

    /// <inheritdoc />
    public void LogWarning(string message, IReadOnlyDictionary<string, object?>? context = null)
        => Write("Warning", message, null, context);

    /// <inheritdoc />
    public void LogError(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? context = null)
        => Write("Error", message, exception, context);

    private void Write(string level, string message, Exception? exception, IReadOnlyDictionary<string, object?>? context)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must be provided.", nameof(message));
        }

        var correlationId = AdapterCorrelationScope.CurrentCorrelationId ?? Guid.NewGuid().ToString("N");
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (context is not null)
        {
            foreach (var kvp in context)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key))
                {
                    payload[kvp.Key] = kvp.Value;
                }
            }
        }

        if (!payload.ContainsKey("CorrelationId"))
        {
            payload["CorrelationId"] = correlationId;
        }

        var record = new AdapterLogRecord(
            level,
            message,
            exception,
            payload,
            _timestampProvider(),
            correlationId);

        foreach (var sink in _sinks)
        {
            try
            {
                sink.Emit(record);
            }
            catch
            {
                // Prevent logging failures from impacting primary workflows.
            }
        }
    }
}
