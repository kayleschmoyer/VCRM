// VehicleRealtimeService.cs: Coordinates vehicle-related realtime notifications.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts.Realtime;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace CRMAdapter.UI.Services.Realtime;

/// <summary>
/// Publishes vehicle addition events to interested components.
/// </summary>
public sealed class VehicleRealtimeService : IAsyncDisposable
{
    private readonly RealtimeHubConnection _hubConnection;
    private readonly ILogger<VehicleRealtimeService> _logger;
    private readonly ISnackbar _snackbar;
    private readonly List<Action<VehicleAddedEvent>> _vehicleHandlers = new();
    private readonly IDisposable _vehicleSubscription;

    public VehicleRealtimeService(
        RealtimeHubConnection hubConnection,
        ILogger<VehicleRealtimeService> logger,
        ISnackbar snackbar)
    {
        _hubConnection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));

        _vehicleSubscription = _hubConnection.RegisterVehicleAdded(HandleVehicleAdded);
    }

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        return _hubConnection.EnsureConnectedAsync(cancellationToken);
    }

    public IDisposable OnVehicleAdded(Action<VehicleAddedEvent> callback) => Register(_vehicleHandlers, callback);

    private void HandleVehicleAdded(VehicleAddedEvent payload)
    {
        _logger.LogInformation("Vehicle {Vin} added for {CustomerName}.", payload.Vin, payload.CustomerName);
        _snackbar.Add($"Vehicle {payload.Vin} added for {payload.CustomerName}", Severity.Info);
        Notify(_vehicleHandlers, payload);
    }

    private static IDisposable Register<T>(ICollection<Action<T>> handlers, Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(callback);

        lock (handlers)
        {
            handlers.Add(callback);
        }

        return new Subscription(() =>
        {
            lock (handlers)
            {
                handlers.Remove(callback);
            }
        });
    }

    private static void Notify<T>(ICollection<Action<T>> handlers, T payload)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        Action<T>[] snapshot;
        lock (handlers)
        {
            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                handler(payload);
            }
            catch
            {
                // Ignore downstream errors to keep other subscribers alive.
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _vehicleSubscription.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;
        private bool _isDisposed;

        public Subscription(Action onDispose)
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _onDispose();
        }
    }
}
