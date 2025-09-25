// SecretsResolver.cs: Coordinates retrieval of secrets from the configured provider.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Helper responsible for fetching all required secrets and transforming them into a strongly typed model.
/// </summary>
public sealed class SecretsResolver
{
    private readonly ISecretsProvider _secretsProvider;
    private readonly ILogger<SecretsResolver> _logger;

    public SecretsResolver(ISecretsProvider secretsProvider, ILogger<SecretsResolver> logger)
    {
        _secretsProvider = secretsProvider ?? throw new ArgumentNullException(nameof(secretsProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves the secret material required by the host.
    /// </summary>
    public async Task<ResolvedSecrets> ResolveAsync(SecuritySettings settings, CancellationToken cancellationToken = default)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        settings.Validate();

        var sqlSecretNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VastDesktop"] = settings.Secrets.Sql.VastDesktop,
            ["VastOnline"] = settings.Secrets.Sql.VastOnline,
            ["Audit"] = settings.Secrets.Sql.Audit,
        };

        var apiSecretNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientId"] = settings.Secrets.Api.ClientId,
            ["ClientSecret"] = settings.Secrets.Api.ClientSecret,
        };

        var requiredSecretNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            settings.Secrets.JwtSigningKey,
            settings.EncryptionKeyId,
        };

        foreach (var name in sqlSecretNames.Values.Concat(settings.PreviousEncryptionKeyIds))
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                requiredSecretNames.Add(name);
            }
        }

        var optionalSecretNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in apiSecretNames.Values)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                optionalSecretNames.Add(name);
            }
        }

        var requestedSecretNames = new HashSet<string>(requiredSecretNames, StringComparer.OrdinalIgnoreCase);
        foreach (var name in optionalSecretNames)
        {
            requestedSecretNames.Add(name);
        }

        var secretDictionary = await _secretsProvider.GetSecretsAsync(requestedSecretNames, cancellationToken).ConfigureAwait(false);

        var missingSecrets = requiredSecretNames
            .Where(name => !secretDictionary.ContainsKey(name) || string.IsNullOrWhiteSpace(secretDictionary[name]))
            .ToArray();

        if (missingSecrets.Length > 0)
        {
            var scrubbed = string.Join(", ", missingSecrets.Select(name => name));
            throw new SecurityException($"Missing required secrets: {scrubbed}.");
        }

        var sqlConnections = sqlSecretNames.ToDictionary(
            kvp => kvp.Key,
            kvp => secretDictionary[kvp.Value],
            StringComparer.OrdinalIgnoreCase);

        var apiCredentials = apiSecretNames
            .Where(kvp => secretDictionary.TryGetValue(kvp.Value, out var value) && !string.IsNullOrWhiteSpace(value))
            .ToDictionary(kvp => kvp.Key, kvp => secretDictionary[kvp.Value], StringComparer.OrdinalIgnoreCase);

        var encryptionKeys = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var activeKey = secretDictionary[settings.EncryptionKeyId];
        encryptionKeys[settings.EncryptionKeyId] = DecodeKey(settings.EncryptionKeyId, activeKey);

        foreach (var legacyKey in settings.PreviousEncryptionKeyIds)
        {
            if (secretDictionary.TryGetValue(legacyKey, out var legacyValue) && !string.IsNullOrWhiteSpace(legacyValue))
            {
                encryptionKeys[legacyKey] = DecodeKey(legacyKey, legacyValue);
            }
        }

        var resolved = new ResolvedSecrets(
            jwtSigningKey: secretDictionary[settings.Secrets.JwtSigningKey],
            sqlConnections: sqlConnections,
            apiCredentials: apiCredentials,
            encryptionKeys: encryptionKeys);

        _logger.LogInformation("Secrets resolved successfully for {SqlConnectionCount} SQL targets and {EncryptionKeyCount} encryption keys.", sqlConnections.Count, encryptionKeys.Count);

        return resolved;
    }

    private static byte[] DecodeKey(string keyId, string encodedValue)
    {
        try
        {
            return Convert.FromBase64String(encodedValue);
        }
        catch (FormatException ex)
        {
            throw new SecurityException(FormattableString.Invariant($"Encryption key '{keyId}' is not a valid base64 string."), ex);
        }
    }
}
