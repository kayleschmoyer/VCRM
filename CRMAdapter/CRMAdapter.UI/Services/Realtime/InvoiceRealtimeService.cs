// InvoiceRealtimeService.cs: Surfaces invoice realtime events with role-aware notifications.
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
/// Provides typed callbacks for invoice-related realtime events.
/// </summary>
public sealed class InvoiceRealtimeService : IAsyncDisposable
{
    private readonly RealtimeHubConnection _hubConnection;
    private readonly AuthStateProvider _authStateProvider;
    private readonly ILogger<InvoiceRealtimeService> _logger;
    private readonly ISnackbar _snackbar;
    private readonly List<Action<InvoiceCreatedEvent>> _createdHandlers = new();
    private readonly List<Action<InvoicePaidEvent>> _paidHandlers = new();
    private readonly IDisposable _createdSubscription;
    private readonly IDisposable _paidSubscription;

    public InvoiceRealtimeService(
        RealtimeHubConnection hubConnection,
        AuthStateProvider authStateProvider,
        ILogger<InvoiceRealtimeService> logger,
        ISnackbar snackbar)
    {
        _hubConnection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));
        _authStateProvider = authStateProvider ?? throw new ArgumentNullException(nameof(authStateProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));

        _createdSubscription = _hubConnection.RegisterInvoiceCreated(HandleInvoiceCreated);
        _paidSubscription = _hubConnection.RegisterInvoicePaid(HandleInvoicePaid);
    }

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        return _hubConnection.EnsureConnectedAsync(cancellationToken);
    }

    public IDisposable OnInvoiceCreated(Action<InvoiceCreatedEvent> callback) => Register(_createdHandlers, callback);

    public IDisposable OnInvoicePaid(Action<InvoicePaidEvent> callback) => Register(_paidHandlers, callback);

    private void HandleInvoiceCreated(InvoiceCreatedEvent payload)
    {
        _logger.LogInformation("Invoice {InvoiceNumber} created for {CustomerName}.", payload.InvoiceNumber, payload.CustomerName);
        var formattedTotal = payload.Total.ToString("C", CultureInfo.CurrentCulture);
        _snackbar.Add($"Invoice {payload.InvoiceNumber} issued · {formattedTotal}", Severity.Info);
        Notify(_createdHandlers, payload);
    }

    private void HandleInvoicePaid(InvoicePaidEvent payload)
    {
        _logger.LogInformation("Invoice {InvoiceNumber} paid by {CustomerName}.", payload.InvoiceNumber, payload.CustomerName);
        var isAdmin = _authStateProvider.CurrentUser.IsInRole(RbacRole.Admin.ToString());
        var message = isAdmin
            ? $"Invoice #{payload.InvoiceNumber} just paid"
            : $"Payment received for invoice #{payload.InvoiceNumber}";
        _snackbar.Add(message, Severity.Success);
        Notify(_paidHandlers, payload);
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
        _paidSubscription.Dispose();
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
