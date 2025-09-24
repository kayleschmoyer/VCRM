/*
 * File: IAdapterLogger.cs
 * Purpose: Defines the logging abstraction consumed by all adapters for structured, dependency-injected telemetry.
 * Security Considerations: Prevents sensitive data from leaking by centralizing redaction and enforcing safe logging patterns.
 * Example Usage: `logger.LogError("Customer lookup failed", exception: ex, context: new { CustomerId = id });`
 */
using System;
using System.Collections.Generic;

namespace CRMAdapter.CommonInfrastructure
{
    /// <summary>
    /// Provides structured logging capabilities to adapter components without binding to a specific logging framework.
    /// </summary>
    public interface IAdapterLogger
    {
        /// <summary>
        /// Writes a verbose diagnostic message.
        /// </summary>
        /// <param name="message">Human readable message (should be sanitized).</param>
        /// <param name="context">Optional structured payload.</param>
        void LogDebug(string message, IReadOnlyDictionary<string, object?>? context = null);

        /// <summary>
        /// Writes an informational message useful for auditors.
        /// </summary>
        /// <param name="message">Human readable message (should be sanitized).</param>
        /// <param name="context">Optional structured payload.</param>
        void LogInformation(string message, IReadOnlyDictionary<string, object?>? context = null);

        /// <summary>
        /// Writes a warning message indicating potential risk or throttling.
        /// </summary>
        /// <param name="message">Human readable message (should be sanitized).</param>
        /// <param name="context">Optional structured payload.</param>
        void LogWarning(string message, IReadOnlyDictionary<string, object?>? context = null);

        /// <summary>
        /// Writes an error message capturing sanitized failure information.
        /// </summary>
        /// <param name="message">Human readable message (should be sanitized).</param>
        /// <param name="exception">Underlying exception for diagnostics.</param>
        /// <param name="context">Optional structured payload.</param>
        void LogError(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? context = null);
    }

    /// <summary>
    /// Provides a safe do-nothing logger used when an adapter is not supplied with a logging implementation.
    /// </summary>
    public sealed class NullAdapterLogger : IAdapterLogger
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static NullAdapterLogger Instance { get; } = new();

        private NullAdapterLogger()
        {
        }

        /// <inheritdoc />
        public void LogDebug(string message, IReadOnlyDictionary<string, object?>? context = null)
        {
        }

        /// <inheritdoc />
        public void LogInformation(string message, IReadOnlyDictionary<string, object?>? context = null)
        {
        }

        /// <inheritdoc />
        public void LogWarning(string message, IReadOnlyDictionary<string, object?>? context = null)
        {
        }

        /// <inheritdoc />
        public void LogError(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? context = null)
        {
        }
    }
}
