// EnvSecretsProvider.cs: Resolves secrets from environment variables for development and CI.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Retrieves secrets from environment variables.
/// </summary>
public sealed class EnvSecretsProvider : ISecretsProvider
{
    private readonly ILogger<EnvSecretsProvider> _logger;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public EnvSecretsProvider(ILogger<EnvSecretsProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<string?>(null);
        }

        if (_cache.TryGetValue(name, out var cached))
        {
            return Task.FromResult<string?>(cached);
        }

        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            _cache[name] = value;
            _logger.LogInformation("Environment secret {SecretName} resolved.", name);
        }
        else
        {
            _logger.LogWarning("Environment secret {SecretName} not found.", name);
        }

        return Task.FromResult<string?>(value);
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
}
