// CorrelationContext.cs: Generates and exposes the current request correlation identifier for diagnostics UI elements.
using System;
using System.Diagnostics;

namespace CRMAdapter.UI.Services.Diagnostics;

public sealed class CorrelationContext
{
    private string _currentCorrelationId = CreateCorrelationId();

    public string CurrentCorrelationId => _currentCorrelationId;

    public string ShortCorrelationId => _currentCorrelationId.Length > 12
        ? _currentCorrelationId[..12].ToUpperInvariant()
        : _currentCorrelationId.ToUpperInvariant();

    public void Refresh()
    {
        _currentCorrelationId = CreateCorrelationId();
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
