// IDataSourceStrategy.cs: Defines how domain services resolve to mock or live implementations at runtime.
using System.Collections.Generic;

namespace CRMAdapter.UI.Core.DataSource;

/// <summary>
/// Provides data source aware resolution for domain service contracts.
/// </summary>
public interface IDataSourceStrategy
{
    /// <summary>
    /// Resolves the concrete implementation for the requested contract.
    /// </summary>
    /// <typeparam name="TService">The service contract type.</typeparam>
    /// <returns>The implementation selected based on the configured mode.</returns>
    TService GetService<TService>() where TService : class;

    /// <summary>
    /// Gets the effective mode for the requested contract, considering overrides.
    /// </summary>
    DataSourceMode GetMode<TService>() where TService : class;

    /// <summary>
    /// Enumerates the configured modes keyed by entity name.
    /// </summary>
    IReadOnlyDictionary<string, DataSourceMode> GetConfiguredModes();

    /// <summary>
    /// Applies an override for the entity identified by the provided key.
    /// </summary>
    bool TrySetOverride(string entityKey, DataSourceMode mode);

    /// <summary>
    /// Clears any override for the specified entity key.
    /// </summary>
    void ClearOverride(string entityKey);
}
