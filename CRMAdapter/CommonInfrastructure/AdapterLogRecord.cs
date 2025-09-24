#nullable enable
using System;
using System.Collections.Generic;

namespace CRMAdapter.CommonInfrastructure;

/// <summary>
/// Represents a structured log entry emitted by the adapter infrastructure.
/// </summary>
/// <param name="Level">Log level such as Debug, Information, Warning, or Error.</param>
/// <param name="Message">Human readable message.</param>
/// <param name="Exception">Optional exception associated with the log.</param>
/// <param name="Context">Structured context payload.</param>
/// <param name="Timestamp">Timestamp when the log was created (UTC).</param>
/// <param name="CorrelationId">Ambient correlation identifier associated with the log.</param>
public sealed record AdapterLogRecord(
    string Level,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Context,
    DateTimeOffset Timestamp,
    string? CorrelationId);
