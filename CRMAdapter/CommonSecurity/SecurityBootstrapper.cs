// SecurityBootstrapper.cs: Shared helper to wire security services across hosts.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Provides a consistent bootstrap flow for loading secrets and registering encryption services.
/// </summary>
public static class SecurityBootstrapper
{
    public static async Task<SecurityBootstrapContext> InitializeAsync(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        var settings = new SecuritySettings();
        configuration.GetSection(SecuritySettings.SectionName).Bind(settings);
        settings.Validate();

        var providerLogger = loggerFactory.CreateLogger<VaultSecretsProvider>();
        ISecretsProvider provider = settings.UseKeyVault
            ? new VaultSecretsProvider(settings, providerLogger)
            : new EnvSecretsProvider(loggerFactory.CreateLogger<EnvSecretsProvider>());

        var resolver = new SecretsResolver(provider, loggerFactory.CreateLogger<SecretsResolver>());
        var resolvedSecrets = await resolver.ResolveAsync(settings, cancellationToken).ConfigureAwait(false);

        return new SecurityBootstrapContext(settings, provider, resolvedSecrets);
    }
}

/// <summary>
/// Represents the result of the security bootstrap process.
/// </summary>
public sealed record SecurityBootstrapContext(SecuritySettings Settings, ISecretsProvider Provider, ResolvedSecrets Secrets);
