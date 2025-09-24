// ConnectivityMonitor.cs: Bridges browser online/offline events into the shared sync state.
using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace CRMAdapter.UI.Core.Sync;

public sealed class ConnectivityMonitor : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly OfflineSyncState _state;
    private DotNetObjectReference<ConnectivityMonitor>? _dotNetRef;
    private IJSObjectReference? _module;

    public ConnectivityMonitor(IJSRuntime jsRuntime, OfflineSyncState state)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public async Task InitializeAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/offlineCache.js");
        _dotNetRef ??= DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("registerConnectivity", _dotNetRef);
    }

    [JSInvokable]
    public void UpdateOnlineStatus(bool isOnline)
    {
        _state.SetOffline(!isOnline);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("disposeConnectivity");
            }
            catch (JSDisconnectedException)
            {
                // Ignore: circuit already gone.
            }

            await _module.DisposeAsync();
        }

        _dotNetRef?.Dispose();
    }
}
