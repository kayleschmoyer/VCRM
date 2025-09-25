// AuditLogger.cs: Centralized coordinator that emits structured audit events to configured sinks.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Emits normalized audit events to the configured sinks while enforcing compliance requirements.
/// </summary>
public sealed class AuditLogger
{
    private readonly IReadOnlyCollection<IAuditSink> _sinks;
    private readonly ILogger<AuditLogger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogger"/> class.
    /// </summary>
    /// <param name="sinks">Collection of audit sinks registered for the current host.</param>
    /// <param name="logger">Logger used to report sink failures.</param>
    public AuditLogger(IEnumerable<IAuditSink> sinks, ILogger<AuditLogger> logger)
    {
        if (sinks is null)
        {
            throw new ArgumentNullException(nameof(sinks));
        }

        _sinks = new ReadOnlyCollection<IAuditSink>(sinks.ToArray());
        if (_sinks.Count == 0)
        {
            throw new InvalidOperationException("At least one audit sink must be registered.");
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Persists a structured audit event to every configured sink.
    /// </summary>
    /// <param name="auditEvent">Event payload to persist.</param>
    /// <param name="cancellationToken">Token used to cancel downstream writes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (auditEvent is null)
        {
            throw new ArgumentNullException(nameof(auditEvent));
        }

        var normalized = Normalize(auditEvent);

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.WriteAsync(normalized, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit sink {Sink} failed for action {Action} with correlation {CorrelationId}.", sink.GetType().Name, normalized.Action, normalized.CorrelationId);
            }
        }
    }

    private static AuditEvent Normalize(AuditEvent auditEvent)
    {
        var timestamp = auditEvent.Timestamp == default
            ? DateTimeOffset.UtcNow
            : auditEvent.Timestamp.ToUniversalTime();

        var correlationId = string.IsNullOrWhiteSpace(auditEvent.CorrelationId)
            ? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
            : auditEvent.CorrelationId.Trim();

        var userId = MaskIfSensitive(auditEvent.UserId);
        var role = string.IsNullOrWhiteSpace(auditEvent.Role)
            ? "unknown"
            : auditEvent.Role.Trim();

        var metadata = auditEvent.Metadata is null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(auditEvent.Metadata));

        return auditEvent with
        {
            Timestamp = timestamp,
            CorrelationId = correlationId,
            UserId = userId,
            Role = role,
            Metadata = metadata,
        };
    }

    private static string MaskIfSensitive(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "anonymous";
        }

        // Redact everything but the final four characters to avoid exposing PII.
        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        var visibleTail = trimmed[^4..];
        return string.Concat(new string('*', trimmed.Length - 4), visibleTail);
    }
}

/// <summary>
/// Structured representation of a single security-relevant action.
/// </summary>
/// <param name="CorrelationId">Identifier used to correlate activity across services.</param>
/// <param name="UserId">Masked identifier for the initiating principal.</param>
/// <param name="Role">Role or profile assigned to the principal.</param>
/// <param name="Action">Business action performed by the user.</param>
/// <param name="EntityId">Identifier of the entity being acted upon, if any.</param>
/// <param name="Timestamp">UTC timestamp captured at the time of the event.</param>
/// <param name="Result">Outcome of the attempted action.</param>
/// <param name="Metadata">Optional supplementary, non-sensitive metadata.</param>
public sealed record AuditEvent(
    string CorrelationId,
    string UserId,
    string Role,
    string Action,
    string? EntityId,
    DateTimeOffset Timestamp,
    AuditResult Result,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>
    /// Serializes the event to canonical JSON representation.
    /// </summary>
    public string ToJson(JsonSerializerOptions? options = null)
    {
        options ??= DefaultSerializerOptions.Value;
        return JsonSerializer.Serialize(this, options);
    }

    private static readonly Lazy<JsonSerializerOptions> DefaultSerializerOptions = new(() =>
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    });
}

/// <summary>
/// Canonical set of outcomes supported by the audit log schema.
/// </summary>
public enum AuditResult
{
    /// <summary>
    /// The action completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The action failed due to a handled or unhandled error.
    /// </summary>
    Failure,

    /// <summary>
    /// The action was denied by policy enforcement.
    /// </summary>
    Denied,
}

/// <summary>
/// Configuration describing the audit sink to be used by the current process.
/// </summary>
public sealed class AuditSettings
{
    /// <summary>
    /// Name of the configuration section containing audit settings.
    /// </summary>
    public const string SectionName = "Audit";

    /// <summary>
    /// Gets or sets the sink identifier to activate.
    /// </summary>
    public string Sink { get; set; } = AuditSinkNames.Console;

    /// <summary>
    /// Gets or sets configuration applicable to the file sink.
    /// </summary>
    public FileSinkSettings File { get; set; } = new();

    /// <summary>
    /// Gets or sets configuration applicable to the SQL sink.
    /// </summary>
    public SqlSinkSettings Sql { get; set; } = new();

    /// <summary>
    /// Gets or sets configuration applicable to the console sink.
    /// </summary>
    public ConsoleSinkSettings Console { get; set; } = new();

    /// <summary>
    /// Strongly typed settings for the file audit sink.
    /// </summary>
    public sealed class FileSinkSettings
    {
        /// <summary>
        /// Gets or sets the absolute or relative path to the audit log file.
        /// </summary>
        public string FilePath { get; set; } = "logs/audit.log";
    }

    /// <summary>
    /// Strongly typed settings for the SQL audit sink.
    /// </summary>
    public sealed class SqlSinkSettings
    {
        /// <summary>
        /// Gets or sets the connection string used to reach the audit database.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified table name used to store audit events.
        /// </summary>
        public string TableName { get; set; } = "AuditEvents";
    }

    /// <summary>
    /// Strongly typed settings for the console audit sink.
    /// </summary>
    public sealed class ConsoleSinkSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to emit ANSI color codes.
        /// </summary>
        public bool UseConsoleColors { get; set; } = false;
    }
}

/// <summary>
/// Known sink identifiers used to select concrete implementations.
/// </summary>
public static class AuditSinkNames
{
    /// <summary>
    /// File-based audit sink identifier.
    /// </summary>
    public const string File = "File";

    /// <summary>
    /// SQL-based audit sink identifier.
    /// </summary>
    public const string Sql = "Sql";

    /// <summary>
    /// Console-based audit sink identifier.
    /// </summary>
    public const string Console = "Console";
}
