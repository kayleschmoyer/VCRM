// ILocalCache.cs: Abstraction over platform-specific persistence for offline-ready data and sync queues.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.UI.Core.Storage;

/// <summary>
/// Provides lightweight CRUD semantics over a local persistence mechanism (IndexedDB, filesystem, etc.).
/// </summary>
public interface ILocalCache
{
    /// <summary>
    /// Retrieves an item previously stored for the specified type and key.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the supplied item for later retrieval.
    /// </summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the cached entry if it exists.
    /// </summary>
    Task DeleteAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all cached entries for the specified type.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken cancellationToken = default);
}
