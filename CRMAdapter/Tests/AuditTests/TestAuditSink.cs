// TestAuditSink.cs: Lightweight in-memory audit sink used for verification scenarios.
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;

namespace CRMAdapter.Tests.AuditTests;

internal sealed class TestAuditSink : IAuditSink
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public IReadOnlyCollection<AuditEvent> Events => _events.ToArray();

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(auditEvent);
        return Task.CompletedTask;
    }
}
