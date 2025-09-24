// CustomerRealtimeService.cs: Provides strongly typed callbacks for customer-specific realtime events.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts.Realtime;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace CRMAdapter.UI.Services.Realtime;

/// <summary>
/// Coordinates customer realtime events and surfaces them to Blazor components.
/// </summary>
public sealed class CustomerRealtimeService : IAsyncDisposable
{
    private readonly RealtimeHubConnection _hubConnection;
    private readonly ILogger<CustomerRealtimeService> _logger;
    private readonly ISnackbar _snackbar;
    private readonly List<Action<CustomerCreatedEvent>> _createdHandlers = new();
    private readonly List<Action<CustomerUpdatedEvent>> _updatedHandlers = new();
    private readonly IDisposable _createdSubscription;
    private readonly IDisposable _updatedSubscription;

    public CustomerRealtimeService(
        RealtimeHubConnection hubConnection,
        ILogger<CustomerRealtimeService> logger,
        ISnackbar snackbar)
    {
        _hubConnection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));

        _createdSubscription = _hubConnection.RegisterCustomerCreated(HandleCustomerCreated);
        _updatedSubscription = _hubConnection.RegisterCustomerUpdated(HandleCustomerUpdated);
    }

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        return _hubConnection.EnsureConnectedAsync(cancellationToken);
    }

    public IDisposable OnCustomerCreated(Action<CustomerCreatedEvent> callback) => Register(_createdHandlers, callback);

    public IDisposable OnCustomerUpdated(Action<CustomerUpdatedEvent> callback) => Register(_updatedHandlers, callback);

    private void HandleCustomerCreated(CustomerCreatedEvent payload)
    {
        _logger.LogInformation("Received CustomerCreated event for {CustomerName}.", payload.Name);
        _snackbar.Add($"New customer added: {payload.Name}", Severity.Success);
        Notify(_createdHandlers, payload);
    }

    private void HandleCustomerUpdated(CustomerUpdatedEvent payload)
    {
        _logger.LogInformation("Received CustomerUpdated event for {CustomerName}.", payload.Name);
        var vehicleMessage = payload.VehicleCount == 1 ? "vehicle" : "vehicles";
        _snackbar.Add($"Updated {payload.Name} · {payload.VehicleCount} {vehicleMessage}", Severity.Info);
        Notify(_updatedHandlers, payload);
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
        _createdSubscription.Dispose();
        _updatedSubscription.Dispose();
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
