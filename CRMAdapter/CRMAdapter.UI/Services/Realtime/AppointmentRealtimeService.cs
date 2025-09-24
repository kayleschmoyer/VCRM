// AppointmentRealtimeService.cs: Bridges appointment realtime notifications into UI callbacks.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts.Realtime;
using CRMAdapter.CommonSecurity;
using CRMAdapter.UI.Auth;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace CRMAdapter.UI.Services.Realtime;

/// <summary>
/// Dispatches appointment scheduling events to interested components with role-aware notifications.
/// </summary>
public sealed class AppointmentRealtimeService : IAsyncDisposable
{
    private readonly RealtimeHubConnection _hubConnection;
    private readonly AuthStateProvider _authStateProvider;
    private readonly ILogger<AppointmentRealtimeService> _logger;
    private readonly ISnackbar _snackbar;
    private readonly List<Action<AppointmentScheduledEvent>> _appointmentHandlers = new();
    private readonly IDisposable _appointmentSubscription;

    public AppointmentRealtimeService(
        RealtimeHubConnection hubConnection,
        AuthStateProvider authStateProvider,
        ILogger<AppointmentRealtimeService> logger,
        ISnackbar snackbar)
    {
        _hubConnection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));
        _authStateProvider = authStateProvider ?? throw new ArgumentNullException(nameof(authStateProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));

        _appointmentSubscription = _hubConnection.RegisterAppointmentScheduled(HandleAppointmentScheduled);
    }

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        return _hubConnection.EnsureConnectedAsync(cancellationToken);
    }

    public IDisposable OnAppointmentScheduled(Action<AppointmentScheduledEvent> callback) => Register(_appointmentHandlers, callback);

    private void HandleAppointmentScheduled(AppointmentScheduledEvent payload)
    {
        _logger.LogInformation("Appointment scheduled for {CustomerName} on {Scheduled}.", payload.CustomerName, payload.ScheduledFor);
        var localTime = payload.ScheduledFor.ToLocalTime().ToString("h:mm tt", CultureInfo.CurrentCulture);
        var isClerk = _authStateProvider.CurrentUser.IsInRole(RbacRole.Clerk.ToString());
        var message = isClerk
            ? $"New appointment scheduled at {localTime}"
            : $"Appointment booked for {payload.CustomerName} · {localTime}";
        _snackbar.Add(message, Severity.Info);
        Notify(_appointmentHandlers, payload);
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
        _appointmentSubscription.Dispose();
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
