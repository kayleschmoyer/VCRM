// ConsoleAuditSink.cs: Emits audit events to the console for development diagnostics.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Writes audit events to STDOUT using structured JSON payloads.
/// </summary>
public sealed class ConsoleAuditSink : IAuditSink
{
    private readonly bool _useColors;
    private readonly ILogger<ConsoleAuditSink> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleAuditSink"/> class.
    /// </summary>
    public ConsoleAuditSink(IOptions<AuditSettings> settingsAccessor, ILogger<ConsoleAuditSink> logger)
    {
        if (settingsAccessor is null)
        {
            throw new ArgumentNullException(nameof(settingsAccessor));
        }

        _useColors = settingsAccessor.Value?.Console.UseConsoleColors ?? false;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = auditEvent.ToJson();
            if (_useColors)
            {
                var original = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Out.WriteLine(payload);
                Console.ForegroundColor = original;
            }
            else
            {
                Console.Out.WriteLine(payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit event to console.");
        }

        return Task.CompletedTask;
    }
}
