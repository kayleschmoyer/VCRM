// SyncQueue.cs: Durable offline queue implementation backed by the local cache abstraction.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Core.Sync;

public sealed class SyncQueue : ISyncQueue
{
    private readonly ILocalCache _cache;
    private readonly OfflineSyncState _state;
    private readonly ILogger<SyncQueue> _logger;

    public SyncQueue(ILocalCache cache, OfflineSyncState state, ILogger<SyncQueue> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnqueueChangeAsync(ChangeEnvelope change, CancellationToken cancellationToken = default)
    {
        if (change is null)
        {
            throw new ArgumentNullException(nameof(change));
        }

        _logger.LogInformation("Queuing offline change {ChangeId} for {EntityType} {EntityId} ({Operation}).", change.CorrelationId, change.EntityType, change.EntityId, change.Operation);
        await _cache.SetAsync(change.CorrelationId.ToString(), change, cancellationToken).ConfigureAwait(false);
        await UpdateQueueLengthAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ChangeEnvelope>> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _cache.GetAllAsync<ChangeEnvelope>(cancellationToken).ConfigureAwait(false);
        var ordered = pending.OrderBy(change => change.Timestamp).ToList();
        _logger.LogDebug("DequeueAll requested. {Count} pending changes discovered.", ordered.Count);
        return ordered;
    }

    public async Task MarkSyncedAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        if (correlationId == Guid.Empty)
        {
            return;
        }

        await _cache.DeleteAsync<ChangeEnvelope>(correlationId.ToString(), cancellationToken).ConfigureAwait(false);
        await UpdateQueueLengthAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetLengthAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _cache.GetAllAsync<ChangeEnvelope>(cancellationToken).ConfigureAwait(false);
        return pending.Count;
    }

    private async Task UpdateQueueLengthAsync(CancellationToken cancellationToken)
    {
        var length = await GetLengthAsync(cancellationToken).ConfigureAwait(false);
        _state.SetQueueLength(length);
    }
}
