// ISecretsProvider.cs: Abstraction for retrieving secret material from different backends.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Represents a provider capable of retrieving secret values by logical name.
/// </summary>
public interface ISecretsProvider
{
    /// <summary>
    /// Retrieves the value of a single secret name.
    /// </summary>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple secret values in bulk.
    /// </summary>
    Task<IDictionary<string, string>> GetSecretsAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
}
