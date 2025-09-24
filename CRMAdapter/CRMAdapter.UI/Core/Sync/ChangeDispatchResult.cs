// ChangeDispatchResult.cs: Captures the outcome of attempting to replay an offline change.
using System;

namespace CRMAdapter.UI.Core.Sync;

public sealed record ChangeDispatchResult(bool Success, bool Conflict, DateTimeOffset? ServerTimestamp = null, string? ServerPayload = null, string? FailureReason = null)
{
    public static ChangeDispatchResult Successful(DateTimeOffset? serverTimestamp = null, string? serverPayload = null)
        => new(true, false, serverTimestamp, serverPayload, null);

    public static ChangeDispatchResult ConflictDetected(DateTimeOffset? serverTimestamp, string? serverPayload, string? reason)
        => new(false, true, serverTimestamp, serverPayload, reason);

    public static ChangeDispatchResult Failed(string? reason)
        => new(false, false, null, null, reason);
}
