/*
 * File: FieldMap.cs
 * Purpose: Loads, validates, and exposes backend schema mappings used by adapters with version-aware enforcement.
 * Security Considerations: Rejects malformed JSON, sanitizes table/column expressions to eliminate SQL injection vectors, and enforces schema version compatibility before adapters execute.
 * Example Usage: `var fieldMap = FieldMap.LoadFromFile("/secure/mapping.json"); var idColumn = fieldMap.GetTarget("Customer.Id");`
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CRMAdapter.CommonConfig
{
    /// <summary>
    /// Represents an immutable mapping between canonical fields and backend schema expressions.
    /// </summary>
    public sealed class FieldMap
    {
        private static readonly Regex SourceRegex = new("^[A-Za-z0-9_.\\[\\]]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ExpressionRegex = new("^[A-Za-z0-9_@.,()\\[\\] ]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex CanonicalKeyRegex = new("^[A-Za-z0-9_.]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Version SupportedSchemaVersion = new(1, 0);

        private readonly IReadOnlyDictionary<string, string> _mappings;

        private FieldMap(string backendName, Version schemaVersion, IReadOnlyDictionary<string, string> mappings)
        {
            BackendName = backendName;
            SchemaVersion = schemaVersion;
            _mappings = mappings;
        }

        /// <summary>
        /// Gets the backend name as declared in the mapping file.
        /// </summary>
        public string BackendName { get; }

        /// <summary>
        /// Gets the schema version declared in the mapping file.
        /// </summary>
        public Version SchemaVersion { get; }

        /// <summary>
        /// Loads a field map from a JSON file path.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the mapping file.</param>
        /// <returns>A <see cref="FieldMap"/> instance.</returns>
        /// <exception cref="MappingConfigurationException">Thrown when the file is missing or invalid.</exception>
        public static FieldMap LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Mapping file path must be provided.", nameof(filePath));
            }

            var absolutePath = Path.GetFullPath(filePath);

            if (!File.Exists(absolutePath))
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    $"Mapping file '{absolutePath}' was not found.");
            }

            using var stream = File.OpenRead(absolutePath);
            return LoadFromStream(stream);
        }

        /// <summary>
        /// Loads a field map from a stream containing JSON mapping data.
        /// </summary>
        /// <param name="stream">The JSON stream.</param>
        /// <returns>A <see cref="FieldMap"/> instance.</returns>
        /// <exception cref="MappingConfigurationException">Thrown when the JSON cannot be parsed or is invalid.</exception>
        public static FieldMap LoadFromStream(Stream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var documentOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 256
            };

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(stream, documentOptions);
            }
            catch (JsonException ex)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    "Mapping JSON could not be parsed safely.",
                    ex);
            }

            using (document)
            {
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        "Mapping JSON must start with an object.");
                }

                var backendName = ExtractBackendName(root);
                var schemaVersion = ExtractSchemaVersion(root);
                EnsureSchemaVersionIsSupported(schemaVersion, backendName);

                var mappingElement = root.TryGetProperty("mappings", out var nested) ? nested : root;
                var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                FlattenJson(string.Empty, mappingElement, mappings);

                if (!mappings.Keys.Any(key => key.EndsWith(".__source", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        "Mapping must declare at least one entity source using the '__source' suffix.");
                }

                return new FieldMap(backendName, schemaVersion, new ReadOnlyDictionary<string, string>(mappings));
            }
        }

        /// <summary>
        /// Retrieves the backend expression for a canonical path.
        /// </summary>
        /// <param name="canonicalPath">Canonical property path (e.g. <c>Customer.Name</c>).</param>
        /// <returns>Backend expression defined in the mapping.</returns>
        /// <exception cref="MappingConfigurationException">Thrown when the mapping is missing.</exception>
        public string GetTarget(string canonicalPath)
        {
            if (string.IsNullOrWhiteSpace(canonicalPath))
            {
                throw new ArgumentException("Canonical path must be provided.", nameof(canonicalPath));
            }

            if (!CanonicalKeyRegex.IsMatch(canonicalPath))
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    $"Canonical path '{canonicalPath}' contains invalid characters.");
            }

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
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(canonicalPath) || !CanonicalKeyRegex.IsMatch(canonicalPath))
            {
                return false;
            }

            if (!_mappings.TryGetValue(canonicalPath, out var resolved))
            {
                return false;
            }

            value = resolved;
            return true;
        }

        /// <summary>
        /// Retrieves a set of mappings for a canonical entity.
        /// </summary>
        /// <param name="entity">Canonical entity name.</param>
        /// <param name="fields">Canonical fields requested.</param>
        /// <returns>Dictionary of canonical field to backend expression.</returns>
        public IReadOnlyDictionary<string, string> GetTargets(string entity, IEnumerable<string> fields)
        {
            if (string.IsNullOrWhiteSpace(entity))
            {
                throw new ArgumentException("Entity name must be provided.", nameof(entity));
            }

            if (fields is null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        $"Field list for entity '{entity}' contains an empty value.");
                }

                var key = $"{entity}.{field}";
                results[field] = GetTarget(key);
            }

            return new ReadOnlyDictionary<string, string>(results);
        }

        /// <summary>
        /// Gets the configured source (table or view) for the entity when defined.
        /// </summary>
        /// <param name="entity">Canonical entity name.</param>
        /// <returns>The backend source name when defined.</returns>
        /// <exception cref="MappingConfigurationException">Thrown when the entity source is missing.</exception>
        public string GetEntitySource(string entity)
        {
            if (string.IsNullOrWhiteSpace(entity))
            {
                throw new ArgumentException("Entity name must be provided.", nameof(entity));
            }

            var key = $"{entity}.__source";
            if (!_mappings.TryGetValue(key, out var value))
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.MissingMapping,
                    $"Entity source for '{entity}' was not found in backend '{BackendName}'.");
            }

            return value;
        }

        private static string ExtractBackendName(JsonElement root)
        {
            if (!root.TryGetProperty("backendName", out var backendElement) || backendElement.ValueKind != JsonValueKind.String)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    "Mapping JSON must declare a 'backendName' string property.");
            }

            var backendName = backendElement.GetString()!.Trim();
            if (backendName.Length == 0)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    "Mapping backend name cannot be empty.");
            }

            return backendName;
        }

        private static Version ExtractSchemaVersion(JsonElement root)
        {
            if (!root.TryGetProperty("schemaVersion", out var versionElement) || versionElement.ValueKind != JsonValueKind.String)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    "Mapping JSON must declare a 'schemaVersion' string property.");
            }

            if (!Version.TryParse(versionElement.GetString(), out var version))
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    "Mapping schemaVersion could not be parsed as a semantic version.");
            }

            return version;
        }

        private static void EnsureSchemaVersionIsSupported(Version schemaVersion, string backendName)
        {
            if (schemaVersion.Major != SupportedSchemaVersion.Major)
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    $"Mapping for backend '{backendName}' targets schema version '{schemaVersion}', which is incompatible with supported major version '{SupportedSchemaVersion.Major}'.");
            }
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

                if (!CanonicalKeyRegex.IsMatch(key))
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        $"Canonical key '{key}' contains invalid characters.");
                }

                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    FlattenJson(key, property.Value, mappings);
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var mappingValue = property.Value.GetString()!;
                    ValidateMappingValue(key, mappingValue);
                    mappings[key] = mappingValue;
                }
                else
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        $"Mapping value for '{key}' must be a string.");
                }
            }
        }

        private static void ValidateMappingValue(string canonicalKey, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    $"Mapping value for '{canonicalKey}' cannot be empty.");
            }

            if (canonicalKey.EndsWith(".__source", StringComparison.OrdinalIgnoreCase))
            {
                if (!SourceRegex.IsMatch(value))
                {
                    throw new MappingConfigurationException(
                        AdapterErrorCodes.InvalidMapping,
                        $"Entity source mapping for '{canonicalKey}' contains invalid characters.");
                }
            }
            else if (!ExpressionRegex.IsMatch(value))
            {
                throw new MappingConfigurationException(
                    AdapterErrorCodes.InvalidMapping,
                    $"Field mapping for '{canonicalKey}' contains disallowed SQL characters.");
            }
        }
    }
}
