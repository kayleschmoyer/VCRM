// FileSystemCache.cs: Stores offline cache artifacts on disk for desktop/server hosted Blazor scenarios.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.UI.Core.Storage;

/// <summary>
/// File-backed implementation of <see cref="ILocalCache"/> that persists JSON payloads per entity type.
/// </summary>
public sealed class FileSystemCache : ILocalCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public FileSystemCache(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Cache root path must be supplied.", nameof(rootPath));
        }

        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key must be supplied.", nameof(key));
        }

        var map = await ReadAllAsync<T>(cancellationToken).ConfigureAwait(false);
        return map.TryGetValue(key, out var value) ? value : default;
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key must be supplied.", nameof(key));
        }

        var map = await ReadAllAsync<T>(cancellationToken).ConfigureAwait(false);
        map[key] = value!;
        await WriteAllAsync(map, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key must be supplied.", nameof(key));
        }

        var map = await ReadAllAsync<T>(cancellationToken).ConfigureAwait(false);
        if (map.Remove(key))
        {
            await WriteAllAsync(map, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken cancellationToken = default)
    {
        var map = await ReadAllAsync<T>(cancellationToken).ConfigureAwait(false);
        return new List<T>(map.Values);
    }

    private string GetPath<T>()
    {
        var typeName = typeof(T).FullName ?? typeof(T).Name;
        var safeName = typeName.Replace('<', '_').Replace('>', '_').Replace(':', '_').Replace('/', '_');
        return Path.Combine(_rootPath, safeName + ".json");
    }

    private SemaphoreSlim GetLock<T>()
    {
        var key = typeof(T).FullName ?? typeof(T).Name;
        return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private async Task<Dictionary<string, T>> ReadAllAsync<T>(CancellationToken cancellationToken)
    {
        var filePath = GetPath<T>();
        var gate = GetLock<T>();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            }

            await using var stream = File.OpenRead(filePath);
            var payload = await JsonSerializer.DeserializeAsync<Dictionary<string, T>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return payload ?? new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task WriteAllAsync<T>(Dictionary<string, T> items, CancellationToken cancellationToken)
    {
        var filePath = GetPath<T>();
        var gate = GetLock<T>();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, items, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
