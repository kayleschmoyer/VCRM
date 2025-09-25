// SecuritySettings.cs: Configuration binding model describing how secrets are stored and retrieved.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Configuration describing how secrets should be resolved for the current host.
/// </summary>
public sealed class SecuritySettings
{
    /// <summary>
    /// Configuration section name used within configuration files.
    /// </summary>
    public const string SectionName = "Security";

    /// <summary>
    /// Gets or sets a value indicating whether a remote vault should be used for secret retrieval.
    /// </summary>
    public bool UseKeyVault { get; set; }

    /// <summary>
    /// Gets or sets the vault provider identifier ("Azure" or "Aws").
    /// </summary>
    public string VaultProvider { get; set; } = "Azure";

    /// <summary>
    /// Gets or sets the vault endpoint URL when using Azure Key Vault.
    /// </summary>
    public string? VaultUrl { get; set; }

    /// <summary>
    /// Gets or sets the AWS region identifier when targeting Secrets Manager.
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// Gets or sets the secret identifier that contains the active encryption key.
    /// </summary>
    public string EncryptionKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifiers for previous encryption keys that remain valid for decryption only.
    /// </summary>
    public IList<string> PreviousEncryptionKeyIds { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the logical secret names used throughout the application.
    /// </summary>
    public SecretNameSettings Secrets { get; set; } = new();

    /// <summary>
    /// Validates the configuration and throws if required values are missing.
    /// </summary>
    public void Validate()
    {
        if (Secrets is null)
        {
            throw new InvalidOperationException("Security.Secrets configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(Secrets.JwtSigningKey))
        {
            throw new InvalidOperationException("Security.Secrets.JwtSigningKey must be configured.");
        }

        if (string.IsNullOrWhiteSpace(Secrets.Sql.VastDesktop)
            || string.IsNullOrWhiteSpace(Secrets.Sql.VastOnline)
            || string.IsNullOrWhiteSpace(Secrets.Sql.Audit))
        {
            throw new InvalidOperationException("Security.Secrets.Sql secret names must be configured for VastDesktop, VastOnline, and Audit.");
        }

        if (UseKeyVault)
        {
            if (string.Equals(VaultProvider, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(VaultUrl))
                {
                    throw new InvalidOperationException("Security.VaultUrl must be supplied when UseKeyVault is enabled for Azure.");
                }
            }
            else if (string.Equals(VaultProvider, "Aws", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(AwsRegion))
                {
                    throw new InvalidOperationException("Security.AwsRegion must be supplied when UseKeyVault targets AWS Secrets Manager.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Security.VaultProvider '{VaultProvider}' is not supported. Expected 'Azure' or 'Aws'.");
            }
        }

        if (string.IsNullOrWhiteSpace(EncryptionKeyId))
        {
            throw new InvalidOperationException("Security.EncryptionKeyId must be provided.");
        }

        if (PreviousEncryptionKeyIds is null)
        {
            PreviousEncryptionKeyIds = new List<string>();
        }
        else
        {
            PreviousEncryptionKeyIds = PreviousEncryptionKeyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

/// <summary>
/// Logical secret names used by the application.
/// </summary>
public sealed class SecretNameSettings
{
    /// <summary>
    /// Gets or sets the JWT signing key secret name.
    /// </summary>
    public string JwtSigningKey { get; set; } = "CRM_JWT_SIGNING_KEY";

    /// <summary>
    /// Gets or sets the SQL secret names.
    /// </summary>
    public SqlSecretNames Sql { get; set; } = new();

    /// <summary>
    /// Gets or sets API credential secret names.
    /// </summary>
    public ApiSecretNames Api { get; set; } = new();
}

/// <summary>
/// Secret name manifest for SQL connection strings.
/// </summary>
public sealed class SqlSecretNames
{
    public string VastDesktop { get; set; } = "CRM_SQL_VAST_DESKTOP";

    public string VastOnline { get; set; } = "CRM_SQL_VAST_ONLINE";

    public string Audit { get; set; } = "CRM_SQL_AUDIT";
}

/// <summary>
/// Secret name manifest for downstream API credentials.
/// </summary>
public sealed class ApiSecretNames
{
    public string ClientId { get; set; } = "CRM_API_CLIENT_ID";

    public string ClientSecret { get; set; } = "CRM_API_CLIENT_SECRET";
}
