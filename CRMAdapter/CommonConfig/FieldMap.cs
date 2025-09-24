/*
 * File: FieldMap.cs
 * Role: Loads and exposes backend schema mappings used by adapters.
 * Architectural Purpose: Centralizes schema translation and keeps adapters free from hardcoded SQL metadata.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CRMAdapter.CommonConfig
{
    /// <summary>
    /// Represents an immutable mapping between canonical fields and backend schema expressions.
    /// </summary>
    public sealed class FieldMap
    {
        private readonly IReadOnlyDictionary<string, string> _mappings;

        private FieldMap(string backendName, IReadOnlyDictionary<string, string> mappings)
        {
            BackendName = backendName;
            _mappings = mappings;
        }

        /// <summary>
        /// Gets the backend name as declared in the mapping file.
        /// </summary>
        public string BackendName { get; }

        /// <summary>
        /// Loads a field map from a JSON file path.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the mapping file.</param>
        /// <returns>A <see cref="FieldMap"/> instance.</returns>
        public static FieldMap LoadFromFile(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return LoadFromStream(stream);
        }

        /// <summary>
        /// Loads a field map from a stream containing JSON mapping data.
        /// </summary>
        /// <param name="stream">The JSON stream.</param>
        /// <returns>A <see cref="FieldMap"/> instance.</returns>
        public static FieldMap LoadFromStream(Stream stream)
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            string backendName = root.TryGetProperty("backendName", out var backendElement) &&
                                 backendElement.ValueKind == JsonValueKind.String
                ? backendElement.GetString()!
                : "Unknown";

            var mappingElement = root.TryGetProperty("mappings", out var nested)
                ? nested
                : root;

            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FlattenJson(string.Empty, mappingElement, mappings);

            return new FieldMap(backendName, new ReadOnlyDictionary<string, string>(mappings));
        }

        /// <summary>
        /// Retrieves the backend expression for a canonical path.
        /// </summary>
        /// <param name="canonicalPath">Canonical property path (e.g. <c>Customer.Name</c>).</param>
        /// <returns>Backend expression defined in the mapping.</returns>
        /// <exception cref="MappingConfigurationException">Thrown when the mapping is missing.</exception>
        public string GetTarget(string canonicalPath)
        {
            if (!_mappings.TryGetValue(canonicalPath, out var value))
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.MissingMapping,
                    $"Mapping for '{canonicalPath}' was not found in backend '{BackendName}'.");
            }

            return value;
        }

        /// <summary>
        /// Tries to get the backend expression for a canonical path.
        /// </summary>
        /// <param name="canonicalPath">Canonical property path.</param>
        /// <param name="value">Resolved backend expression.</param>
        /// <returns><c>true</c> when found; otherwise, <c>false</c>.</returns>
        public bool TryGetTarget(string canonicalPath, out string value)
        {
            return _mappings.TryGetValue(canonicalPath, out value!);
        }

        /// <summary>
        /// Retrieves a set of mappings for a canonical entity.
        /// </summary>
        /// <param name="entity">Canonical entity name.</param>
        /// <param name="fields">Canonical fields requested.</param>
        /// <returns>Dictionary of canonical field to backend expression.</returns>
        public IReadOnlyDictionary<string, string> GetTargets(string entity, IEnumerable<string> fields)
        {
            return fields.ToDictionary(
                field => field,
                field => GetTarget($"{entity}.{field}"),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the configured source (table or view) for the entity when defined.
        /// </summary>
        /// <param name="entity">Canonical entity name.</param>
        /// <returns>The backend source name when defined; otherwise, empty string.</returns>
        public string GetEntitySource(string entity)
        {
            return _mappings.TryGetValue($"{entity}.__source", out var value) ? value : string.Empty;
        }

        private static void FlattenJson(string prefix, JsonElement element, IDictionary<string, string> mappings)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    "Mapping files must contain an object of key/value pairs.");
            }

            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrEmpty(prefix)
                    ? property.Name
                    : string.IsNullOrEmpty(property.Name)
                        ? prefix
                        : $"{prefix}.{property.Name}";

                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    FlattenJson(key, property.Value, mappings);
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    mappings[key] = property.Value.GetString()!;
                }
                else
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        $"Mapping value for '{key}' must be a string.");
                }
            }
        }
    }
}
