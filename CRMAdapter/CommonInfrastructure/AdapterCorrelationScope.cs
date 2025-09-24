#nullable enable
using System;
using System.Threading;

namespace CRMAdapter.CommonInfrastructure;

/// <summary>
/// Maintains an ambient correlation identifier so logs across async flows can be linked together.
/// </summary>
public static class AdapterCorrelationScope
{
    private sealed class Scope : IDisposable
    {
        private readonly Scope? _parent;
        private bool _disposed;

        internal Scope(string correlationId, Scope? parent)
        {
            CorrelationId = correlationId;
            _parent = parent;
        }

        public string CorrelationId { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_current.Value == this)
            {
                _current.Value = _parent;
            }
            else
            {
                _current.Value = _parent;
            }
        }
    }

    private static readonly AsyncLocal<Scope?> _current = new();

    /// <summary>
    /// Gets the ambient correlation identifier if one has been established.
    /// </summary>
    public static string? CurrentCorrelationId => _current.Value?.CorrelationId;

    /// <summary>
    /// Begins a new correlation scope. When no identifier is supplied, the ambient value is reused
    /// if present, otherwise a new identifier is generated.
    /// </summary>
    /// <param name="correlationId">Optional explicit correlation identifier.</param>
    /// <returns>A disposable handle that restores the previous scope when disposed.</returns>
    public static CorrelationScope BeginScope(string? correlationId = null)
    {
        var parent = _current.Value;
        var effectiveId = string.IsNullOrWhiteSpace(correlationId)
            ? parent?.CorrelationId ?? Guid.NewGuid().ToString("N")
            : correlationId;

        var scope = new Scope(effectiveId, parent);
        _current.Value = scope;
        return new CorrelationScope(scope);
    }

    /// <summary>
    /// Represents a disposable correlation scope.
    /// </summary>
    public readonly struct CorrelationScope : IDisposable
    {
        private readonly Scope? _scope;

        internal CorrelationScope(Scope? scope)
        {
            _scope = scope;
        }

        /// <summary>
        /// Gets the correlation identifier associated with the scope.
        /// </summary>
        public string CorrelationId => _scope?.CorrelationId ?? string.Empty;

        /// <inheritdoc />
        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}
