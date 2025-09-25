// SqlAuditSink.cs: Persists audit events to a relational database table using parameterized commands.
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Writes audit events to a SQL table for tamper-evident retention.
/// </summary>
public sealed class SqlAuditSink : IAuditSink
{
    private readonly string? _connectionString;
    private readonly string _tableName;
    private readonly ILogger<SqlAuditSink> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlAuditSink"/> class.
    /// </summary>
    public SqlAuditSink(IOptions<AuditSettings> settingsAccessor, ILogger<SqlAuditSink> logger)
    {
        if (settingsAccessor is null)
        {
            throw new ArgumentNullException(nameof(settingsAccessor));
        }

        var settings = settingsAccessor.Value ?? throw new InvalidOperationException("Audit settings must be configured.");
        _connectionString = settings.Sql.ConnectionString;
        _tableName = string.IsNullOrWhiteSpace(settings.Sql.TableName) ? "AuditEvents" : settings.Sql.TableName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("SQL audit sink is configured without a connection string. Event will be dropped.");
            return;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = $"INSERT INTO {_tableName} (CorrelationId, UserId, Role, Action, EntityId, TimestampUtc, Result, Payload) " +
                                  "VALUES (@CorrelationId, @UserId, @Role, @Action, @EntityId, @TimestampUtc, @Result, @Payload);";
            command.Parameters.AddWithValue("@CorrelationId", auditEvent.CorrelationId);
            command.Parameters.AddWithValue("@UserId", auditEvent.UserId);
            command.Parameters.AddWithValue("@Role", auditEvent.Role);
            command.Parameters.AddWithValue("@Action", auditEvent.Action);
            command.Parameters.AddWithValue("@EntityId", auditEvent.EntityId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TimestampUtc", auditEvent.Timestamp.UtcDateTime);
            command.Parameters.AddWithValue("@Result", auditEvent.Result.ToString());
            command.Parameters.AddWithValue("@Payload", auditEvent.ToJson());

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist audit event to SQL table {AuditTable}.", _tableName);
        }
    }
}
