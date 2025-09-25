// CorrelationContext.cs: Generates and exposes the current request correlation identifier for diagnostics UI elements.
using System;
using System.Diagnostics;

namespace CRMAdapter.UI.Services.Diagnostics;

public sealed class CorrelationContext
{
    /// <summary>
    /// Header name used to propagate correlation identifiers between the UI and API layers.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

    private readonly object _syncRoot = new();
    private string _currentCorrelationId = CreateCorrelationId();

    /// <summary>
    /// Gets the current correlation identifier for outbound operations.
    /// </summary>
    public string CurrentCorrelationId
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentCorrelationId;
            }
        }
    }

    /// <summary>
    /// Gets a shortened version of the current correlation identifier for display purposes.
    /// </summary>
    public string ShortCorrelationId
    {
        get
        {
            var current = CurrentCorrelationId;
            return current.Length > 12
                ? current[..12].ToUpperInvariant()
                : current.ToUpperInvariant();
        }
    }

    /// <summary>
    /// Generates a fresh correlation identifier.
    /// </summary>
    public void Refresh()
    {
        lock (_syncRoot)
        {
            _currentCorrelationId = CreateCorrelationId();
        }
    }

    /// <summary>
    /// Overrides the current correlation identifier if a trusted upstream value is supplied.
    /// </summary>
    /// <param name="correlationId">Correlation identifier provided by the API layer.</param>
    public void SetCorrelationId(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return;
        }

        lock (_syncRoot)
        {
            _currentCorrelationId = correlationId.Trim();
        }
    }

    private static string CreateCorrelationId()
    {
        if (Activity.Current is { Id: { } activityId })
        {
            return activityId;
        }

        return $"CRM-{Guid.NewGuid():N}";
    }
}
