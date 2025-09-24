// OfflineSyncOptions.cs: Configuration model controlling background sync behavior.
namespace CRMAdapter.UI.Core.Sync;

public sealed class OfflineSyncOptions
{
    public const string SectionName = "OfflineSync";

    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 30;
}
