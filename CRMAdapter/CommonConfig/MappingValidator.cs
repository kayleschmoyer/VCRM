/*
 * File: MappingValidator.cs
 * Role: Provides validation helpers for schema mapping files and exposes rich configuration exceptions.
 * Architectural Purpose: Guarantees adapter stability by failing fast when configuration drift is detected.
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMAdapter.CommonConfig
{
    /// <summary>
    /// Validates mapping configurations for adapter requirements.
    /// </summary>
    public static class MappingValidator
    {
        /// <summary>
        /// Ensures the specified canonical keys are present in the <see cref="FieldMap"/>.
        /// </summary>
        /// <param name="fieldMap">Mapping instance.</param>
        /// <param name="canonicalKeys">Canonical keys that must exist.</param>
        /// <param name="adapterName">Adapter name for diagnostic messages.</param>
        public static void EnsureMappings(FieldMap fieldMap, IEnumerable<string> canonicalKeys, string adapterName)
        {
            if (fieldMap is null)
            {
                throw new ArgumentNullException(nameof(fieldMap));
            }

            if (canonicalKeys is null)
            {
                throw new ArgumentNullException(nameof(canonicalKeys));
            }

            var missingKeys = canonicalKeys
                .Where(key => !fieldMap.TryGetTarget(key, out _))
                .ToArray();

            if (missingKeys.Length > 0)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.MissingMapping,
                    $"Adapter '{adapterName}' detected missing mappings: {string.Join(", ", missingKeys)}.");
            }
        }
    }

    /// <summary>
    /// Provides well-known adapter error codes for telemetry correlation.
    /// </summary>
    public static class AdapterErrorCodes
    {
        /// <summary>
        /// Error code for missing mappings.
        /// </summary>
        public const string MissingMapping = "CFG001";

        /// <summary>
        /// Error code for invalid mapping format.
        /// </summary>
        public const string InvalidMapping = "CFG002";

        /// <summary>
        /// Error code for data access failures.
        /// </summary>
        public const string DataAccessFailure = "DAL001";
    }

    /// <summary>
    /// Exception thrown when mapping configuration issues occur.
    /// </summary>
    public sealed class MappingConfigurationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MappingConfigurationException"/> class.
        /// </summary>
        /// <param name="errorCode">Machine friendly error code.</param>
        /// <param name="message">Human readable error message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        public MappingConfigurationException(string errorCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Gets the error code for logging and telemetry.
        /// </summary>
        public string ErrorCode { get; }
    }
}
