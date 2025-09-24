// IndexedDbCache.cs: IndexedDB-backed cache implementation for offline support in browser-hosted Blazor.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace CRMAdapter.UI.Core.Storage;

/// <summary>
/// Implements <see cref="ILocalCache"/> via IndexedDB using a small JS helper shim.
/// </summary>
public sealed class IndexedDbCache : ILocalCache, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public IndexedDbCache(IJSRuntime jsRuntime)
    {
        if (jsRuntime is null)
        {
            throw new ArgumentNullException(nameof(jsRuntime));
        }

        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/offlineCache.js").AsTask());
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key must be supplied.", nameof(key));
        }

        var module = await _moduleTask.Value.ConfigureAwait(false);
        var json = await module.InvokeAsync<string?>("getEntry", cancellationToken, GetTypeKey<T>(), key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key must be supplied.", nameof(key));
        }

        var module = await _moduleTask.Value.ConfigureAwait(false);
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await module.InvokeVoidAsync("setEntry", cancellationToken, GetTypeKey<T>(), key, json);
    }

    public async Task DeleteAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key must be supplied.", nameof(key));
        }

        var module = await _moduleTask.Value.ConfigureAwait(false);
        await module.InvokeVoidAsync("deleteEntry", cancellationToken, GetTypeKey<T>(), key);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value.ConfigureAwait(false);
        var jsonItems = await module.InvokeAsync<string[]>("getAllEntries", cancellationToken, GetTypeKey<T>());
        var list = new List<T>(jsonItems.Length);
        foreach (var json in jsonItems)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                var item = JsonSerializer.Deserialize<T>(json, SerializerOptions);
                if (item is not null)
                {
                    list.Add(item);
                }
            }
        }

        return list;
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value.ConfigureAwait(false);
            await module.DisposeAsync();
        }
    }

    private static string GetTypeKey<T>()
    {
        return typeof(T).FullName ?? typeof(T).Name;
    }
}
