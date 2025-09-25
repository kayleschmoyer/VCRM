// VaultSecretsProvider.cs: Resolves secrets from Azure Key Vault or AWS Secrets Manager.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Retrieves secret material from a cloud-hosted vault.
/// </summary>
public sealed class VaultSecretsProvider : ISecretsProvider
{
    private readonly IVaultAdapter _adapter;
    private readonly ILogger<VaultSecretsProvider> _logger;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public VaultSecretsProvider(SecuritySettings settings, ILogger<VaultSecretsProvider> logger)
        : this(CreateAdapter(settings), logger)
    {
    }

    internal VaultSecretsProvider(IVaultAdapter adapter, ILogger<VaultSecretsProvider> logger)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (_cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var value = await _adapter.GetSecretAsync(name, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(value))
        {
            _cache[name] = value;
            _logger.LogInformation("Vault secret {SecretName} resolved.", name);
        }
        else
        {
            _logger.LogWarning("Vault secret {SecretName} not found.", name);
        }

        return value;
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, string>> GetSecretsAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var value = await GetSecretAsync(name, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static IVaultAdapter CreateAdapter(SecuritySettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        settings.Validate();

        if (string.Equals(settings.VaultProvider, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(settings.VaultUrl))
            {
                throw new InvalidOperationException("VaultUrl must be provided for Azure Key Vault.");
            }

            var vaultUri = new Uri(settings.VaultUrl, UriKind.Absolute);
            var credential = new DefaultAzureCredential();
            var client = new SecretClient(vaultUri, credential);
            return new AzureKeyVaultAdapter(client);
        }

        if (string.Equals(settings.VaultProvider, "Aws", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(settings.AwsRegion))
            {
                throw new InvalidOperationException("AwsRegion must be provided for AWS Secrets Manager.");
            }

            var regionEndpoint = RegionEndpoint.GetBySystemName(settings.AwsRegion);
            var client = new AmazonSecretsManagerClient(regionEndpoint);
            return new AwsSecretsManagerAdapter(client);
        }

        throw new InvalidOperationException($"Unsupported vault provider '{settings.VaultProvider}'.");
    }
}

internal interface IVaultAdapter
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
}

internal sealed class AzureKeyVaultAdapter : IVaultAdapter
{
    private readonly SecretClient _client;

    public AzureKeyVaultAdapter(SecretClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetSecretAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value?.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}

internal sealed class AwsSecretsManagerAdapter : IVaultAdapter
{
    private readonly IAmazonSecretsManager _client;

    public AwsSecretsManagerAdapter(IAmazonSecretsManager client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = name,
            }, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(response.SecretString))
            {
                return response.SecretString;
            }

            if (response.SecretBinary is not null)
            {
                return System.Text.Encoding.UTF8.GetString(response.SecretBinary.ToArray());
            }

            return null;
        }
        catch (ResourceNotFoundException)
        {
            return null;
        }
    }
}
