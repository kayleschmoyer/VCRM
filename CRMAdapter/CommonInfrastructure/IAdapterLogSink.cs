#nullable enable

namespace CRMAdapter.CommonInfrastructure;

/// <summary>
/// Defines a log sink that can receive structured adapter log records.
/// </summary>
public interface IAdapterLogSink
{
    /// <summary>
    /// Emits a structured log record to the sink.
    /// </summary>
    /// <param name="record">Log record to emit.</param>
    void Emit(AdapterLogRecord record);
}
