// FileAuditSink.cs: Persists audit events to a JSON lines file for local inspection.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Writes audit entries to an append-only JSON log on the local filesystem.
/// </summary>
public sealed class FileAuditSink : IAuditSink
{
    private readonly string _filePath;
    private readonly ILogger<FileAuditSink> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAuditSink"/> class.
    /// </summary>
    /// <param name="settingsAccessor">Accessor for audit configuration.</param>
    /// <param name="logger">Logger used to report file write failures.</param>
    public FileAuditSink(IOptions<AuditSettings> settingsAccessor, ILogger<FileAuditSink> logger)
    {
        if (settingsAccessor is null)
        {
            throw new ArgumentNullException(nameof(settingsAccessor));
        }

        var settings = settingsAccessor.Value ?? throw new InvalidOperationException("Audit settings must be configured.");
        if (string.IsNullOrWhiteSpace(settings.File.FilePath))
        {
            throw new InvalidOperationException("Audit file path must be provided when using the file sink.");
        }

        _filePath = settings.File.FilePath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var directory = Path.GetDirectoryName(Path.GetFullPath(_filePath));
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = auditEvent.ToJson() + Environment.NewLine;
            await File.AppendAllTextAsync(_filePath, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append audit event to {AuditFile}.", _filePath);
        }
    }
}
