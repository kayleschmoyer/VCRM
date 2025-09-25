// ResolvedSecrets.cs: Immutable snapshot of secret material resolved during startup.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Represents the resolved secrets required by the application at runtime.
/// </summary>
public sealed class ResolvedSecrets
{
    public ResolvedSecrets(
        string jwtSigningKey,
        IReadOnlyDictionary<string, string> sqlConnections,
        IReadOnlyDictionary<string, string> apiCredentials,
        IReadOnlyDictionary<string, byte[]> encryptionKeys)
    {
        JwtSigningKey = jwtSigningKey ?? throw new ArgumentNullException(nameof(jwtSigningKey));
        SqlConnections = new ReadOnlyDictionary<string, string>(sqlConnections ?? throw new ArgumentNullException(nameof(sqlConnections)));
        ApiCredentials = new ReadOnlyDictionary<string, string>(apiCredentials ?? throw new ArgumentNullException(nameof(apiCredentials)));
        EncryptionKeys = new ReadOnlyDictionary<string, byte[]>(encryptionKeys ?? throw new ArgumentNullException(nameof(encryptionKeys)));
    }

    /// <summary>
    /// Gets the symmetric key used to validate inbound JWT tokens.
    /// </summary>
    public string JwtSigningKey { get; }

    /// <summary>
    /// Gets the SQL connection strings indexed by logical backend name.
    /// </summary>
    public IReadOnlyDictionary<string, string> SqlConnections { get; }

    /// <summary>
    /// Gets API credential material indexed by logical name.
    /// </summary>
    public IReadOnlyDictionary<string, string> ApiCredentials { get; }

    /// <summary>
    /// Gets the encryption keys indexed by key identifier.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> EncryptionKeys { get; }

    /// <summary>
    /// Gets a value indicating whether all critical secrets have been resolved.
    /// </summary>
    public bool IsHealthy => !string.IsNullOrWhiteSpace(JwtSigningKey)
        && SqlConnections.TryGetValue("VastDesktop", out var desktop) && !string.IsNullOrWhiteSpace(desktop)
        && SqlConnections.TryGetValue("VastOnline", out var online) && !string.IsNullOrWhiteSpace(online)
        && EncryptionKeys.Count > 0;
}
