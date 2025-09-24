using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.Storage;
using CRMAdapter.UI.Core.Sync;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMAdapter.UI.Tests.Offline;

public sealed class SyncQueueTests : IDisposable
{
    private readonly string _path;
    private readonly ILocalCache _cache;
    private readonly OfflineSyncState _state = new();

    public SyncQueueTests()
    {
        _path = Path.Combine(Path.GetTempPath(), "crm-sync-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_path);
        _cache = new FileSystemCache(_path);
    }

    [Fact]
    public async Task Enqueue_and_mark_synced_lifecycle()
    {
        var queue = new SyncQueue(_cache, _state, NullLogger<SyncQueue>.Instance);
        var change = ChangeEnvelope.ForUpdate("Customers", Guid.NewGuid().ToString(), new { Name = "Test" });

        await queue.EnqueueChangeAsync(change);
        (await queue.GetLengthAsync()).Should().Be(1);

        var pending = await queue.DequeueAllAsync();
        pending.Should().ContainSingle().Which.CorrelationId.Should().Be(change.CorrelationId);

        await queue.MarkSyncedAsync(change.CorrelationId);
        (await queue.GetLengthAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Dequeue_orders_by_timestamp()
    {
        var queue = new SyncQueue(_cache, _state, NullLogger<SyncQueue>.Instance);
        var changeA = new ChangeEnvelope("Invoices", "A", ChangeOperation.Update, "{}", DateTimeOffset.UtcNow.AddMinutes(-5), Guid.NewGuid());
        var changeB = new ChangeEnvelope("Invoices", "B", ChangeOperation.Update, "{}", DateTimeOffset.UtcNow, Guid.NewGuid());

        await queue.EnqueueChangeAsync(changeB);
        await queue.EnqueueChangeAsync(changeA);

        var pending = await queue.DequeueAllAsync();
        pending.Should().HaveCount(2);
        pending[0].EntityId.Should().Be("A");
        pending[1].EntityId.Should().Be("B");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
        }
        catch
        {
        }
    }
}
