// ChangeEnvelope.cs: Represents a durable offline change pending synchronization.
using System;
using System.Text.Json;

namespace CRMAdapter.UI.Core.Sync;

public sealed class ChangeEnvelope
{
    public ChangeEnvelope(
        string entityType,
        string entityId,
        ChangeOperation operation,
        string? payload,
        DateTimeOffset timestamp,
        Guid correlationId)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        Operation = operation;
        Payload = payload;
        Timestamp = timestamp;
        CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
    }

    public string EntityType { get; }

    public string EntityId { get; }

    public ChangeOperation Operation { get; }

    /// <summary>
    /// Serialized JSON payload representing the requested mutation.
    /// </summary>
    public string? Payload { get; }

    public DateTimeOffset Timestamp { get; }

    public Guid CorrelationId { get; }

    public static ChangeEnvelope ForCreate<T>(string entityType, string entityId, T payload)
    {
        return Create(entityType, entityId, ChangeOperation.Create, payload);
    }

    public static ChangeEnvelope ForUpdate<T>(string entityType, string entityId, T payload)
    {
        return Create(entityType, entityId, ChangeOperation.Update, payload);
    }

    public static ChangeEnvelope ForDelete(string entityType, string entityId)
    {
        return new ChangeEnvelope(entityType, entityId, ChangeOperation.Delete, null, DateTimeOffset.UtcNow, Guid.NewGuid());
    }

    private static ChangeEnvelope Create<T>(string entityType, string entityId, ChangeOperation operation, T payload)
    {
        var json = payload is null
            ? null
            : JsonSerializer.Serialize(payload, payload.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new ChangeEnvelope(entityType, entityId, operation, json, DateTimeOffset.UtcNow, Guid.NewGuid());
    }
}
