// ISyncQueue.cs: Coordinates offline mutation persistence and replay.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.UI.Core.Sync;

public interface ISyncQueue
{
    Task EnqueueChangeAsync(ChangeEnvelope change, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangeEnvelope>> DequeueAllAsync(CancellationToken cancellationToken = default);

    Task MarkSyncedAsync(Guid correlationId, CancellationToken cancellationToken = default);

    Task<int> GetLengthAsync(CancellationToken cancellationToken = default);
}
