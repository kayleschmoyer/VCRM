// IAuditSink.cs: Abstraction for persisting structured audit events to various backends.
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Represents a durable destination for audit events emitted by the platform.
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// Persists the supplied audit event to the underlying storage medium.
    /// </summary>
    /// <param name="auditEvent">Structured event to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
