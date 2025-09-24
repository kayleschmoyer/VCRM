/*
 * File: MappingValidator.cs
 * Purpose: Provides validation helpers for schema mapping files and exposes rich configuration exceptions with schema version checks.
 * Security Considerations: Prevents adapters from starting with stale or tampered mappings by enforcing version compatibility and strict key validation.
 * Example Usage: `MappingValidator.EnsureMappings(fieldMap, requiredKeys, nameof(CustomerAdapter));`
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
        private static readonly Version MinimumSupportedSchemaVersion = new(1, 0);

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

            if (string.IsNullOrWhiteSpace(adapterName))
            {
                throw new ArgumentException("Adapter name must be provided.", nameof(adapterName));
            }

            EnsureSchemaCompatibility(fieldMap, adapterName);

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

        /// <summary>
        /// Ensures that the mapping declares a source for each provided entity.
        /// </summary>
        /// <param name="fieldMap">Mapping instance.</param>
        /// <param name="entities">Entity names requiring sources.</param>
        /// <param name="adapterName">Adapter name for diagnostic messages.</param>
        public static void EnsureEntitySources(FieldMap fieldMap, IEnumerable<string> entities, string adapterName)
        {
            if (fieldMap is null)
            {
                throw new ArgumentNullException(nameof(fieldMap));
            }

            if (entities is null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (string.IsNullOrWhiteSpace(adapterName))
            {
                throw new ArgumentException("Adapter name must be provided.", nameof(adapterName));
            }

            EnsureSchemaCompatibility(fieldMap, adapterName);

            foreach (var entity in entities)
            {
                if (string.IsNullOrWhiteSpace(entity))
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        $"Adapter '{adapterName}' encountered an empty entity name while validating sources.");
                }

                fieldMap.GetEntitySource(entity); // Will throw when missing.
            }
        }

        private static void EnsureSchemaCompatibility(FieldMap fieldMap, string adapterName)
        {
            if (fieldMap.SchemaVersion < MinimumSupportedSchemaVersion)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    $"Adapter '{adapterName}' requires mapping schema version '{MinimumSupportedSchemaVersion}' or above. Current version is '{fieldMap.SchemaVersion}'.");
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
