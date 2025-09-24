// OfflineSyncState.cs: Tracks connectivity and sync metrics for UI feedback.
using System;

namespace CRMAdapter.UI.Core.Sync;

public sealed class OfflineSyncState
{
    private readonly object _gate = new();
    private bool _isOffline;
    private bool _isSyncing;
    private int _queueLength;
    private DateTimeOffset? _lastSuccessfulSync;

    public event Action? StateChanged;
    public event Action<SyncConflictNotification>? ConflictDetected;

    public bool IsOffline
    {
        get
        {
            lock (_gate)
            {
                return _isOffline;
            }
        }
    }

    public bool IsSyncing
    {
        get
        {
            lock (_gate)
            {
                return _isSyncing;
            }
        }
    }

    public int QueueLength
    {
        get
        {
            lock (_gate)
            {
                return _queueLength;
            }
        }
    }

    public DateTimeOffset? LastSuccessfulSync
    {
        get
        {
            lock (_gate)
            {
                return _lastSuccessfulSync;
            }
        }
    }

    public void SetOffline(bool offline)
    {
        lock (_gate)
        {
            if (_isOffline == offline)
            {
                return;
            }

            _isOffline = offline;
        }

        NotifyStateChanged();
    }

    public void SetQueueLength(int length)
    {
        lock (_gate)
        {
            _queueLength = length;
        }

        NotifyStateChanged();
    }

    public void SetSyncing(bool syncing)
    {
        lock (_gate)
        {
            if (_isSyncing == syncing)
            {
                return;
            }

            _isSyncing = syncing;
        }

        NotifyStateChanged();
    }

    public void MarkSuccessfulSync(DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            _lastSuccessfulSync = timestamp;
        }

        NotifyStateChanged();
    }

    public void ReportConflict(SyncConflictNotification notification)
    {
        ConflictDetected?.Invoke(notification);
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}

public sealed record SyncConflictNotification(string EntityType, string EntityId, string? Detail);
