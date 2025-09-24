// RealtimeHubConnection.cs: Manages the SignalR connection lifecycle and dispatches CRM domain events.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts.Realtime;
using CRMAdapter.UI.Auth;
using CRMAdapter.UI.Services.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace CRMAdapter.UI.Services.Realtime;

/// <summary>
/// Provides an abstraction for creating hub connection proxies, enabling easier testing.
/// </summary>
public interface IHubConnectionProxyFactory
{
    /// <summary>Creates a configured hub connection proxy.</summary>
    /// <param name="options">Options used to configure the underlying connection.</param>
    /// <returns>The proxy representing the configured hub connection.</returns>
    IHubConnectionProxy Create(RealtimeHubConnectionOptions options);
}

/// <summary>
/// Represents a minimal abstraction over a SignalR hub connection.
/// </summary>
public interface IHubConnectionProxy : IAsyncDisposable
{
    /// <summary>Gets the current connection state.</summary>
    HubConnectionState State { get; }

    /// <summary>Occurs when the underlying connection is closed.</summary>
    event Func<Exception?, Task>? Closed;

    /// <summary>Occurs when the connection transitions into reconnecting state.</summary>
    event Func<Exception?, Task>? Reconnecting;

    /// <summary>Occurs when the connection successfully re-establishes.</summary>
    event Func<string?, Task>? Reconnected;

    /// <summary>Starts the connection.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the connection.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Registers a handler for a hub method.</summary>
    IDisposable On<T>(string methodName, Action<T> handler);
}

/// <summary>
/// Options used to construct a hub connection proxy instance.
/// </summary>
public sealed class RealtimeHubConnectionOptions
{
    /// <summary>Gets or sets the hub URL to connect to.</summary>
    public required string HubUrl { get; init; }

    /// <summary>Gets or sets the access token provider used for authentication.</summary>
    public Func<Task<string?>>? AccessTokenProvider { get; init; }

    /// <summary>Gets the collection of headers appended to each connection request.</summary>
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Default factory creating SignalR hub connections with exponential reconnect support.
/// </summary>
public sealed class SignalRHubConnectionProxyFactory : IHubConnectionProxyFactory
{
    private readonly ILogger<SignalRHubConnectionProxyFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRHubConnectionProxyFactory"/> class.
    /// </summary>
    /// <param name="logger">Logger used to record factory diagnostics.</param>
    public SignalRHubConnectionProxyFactory(ILogger<SignalRHubConnectionProxyFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IHubConnectionProxy Create(RealtimeHubConnectionOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var builder = new HubConnectionBuilder()
            .WithUrl(options.HubUrl, httpOptions =>
            {
                if (options.AccessTokenProvider is not null)
                {
                    httpOptions.AccessTokenProvider = async () =>
                    {
                        var token = await options.AccessTokenProvider().ConfigureAwait(false);
                        return token ?? string.Empty;
                    };
                }

                foreach (var header in options.Headers)
                {
                    httpOptions.Headers[header.Key] = header.Value;
                }
            })
            .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy());

        _logger.LogInformation("Creating SignalR hub connection targeting {HubUrl}.", options.HubUrl);
        return new SignalRHubConnectionProxy(builder.Build());
    }

    private sealed class SignalRHubConnectionProxy : IHubConnectionProxy
    {
        private readonly HubConnection _connection;

        public SignalRHubConnectionProxy(HubConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public HubConnectionState State => _connection.State;

        public event Func<Exception?, Task>? Closed
        {
            add => _connection.Closed += value;
            remove => _connection.Closed -= value;
        }

        public event Func<Exception?, Task>? Reconnecting
        {
            add => _connection.Reconnecting += value;
            remove => _connection.Reconnecting -= value;
        }

        public event Func<string?, Task>? Reconnected
        {
            add => _connection.Reconnected += value;
            remove => _connection.Reconnected -= value;
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => _connection.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default) => _connection.StopAsync(cancellationToken);

        public IDisposable On<T>(string methodName, Action<T> handler) => _connection.On(methodName, handler);

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext is null)
            {
                throw new ArgumentNullException(nameof(retryContext));
            }

            if (retryContext.PreviousRetryCount >= 6)
            {
                return null;
            }

            var nextDelaySeconds = Math.Pow(2, retryContext.PreviousRetryCount);
            var delay = TimeSpan.FromSeconds(nextDelaySeconds);
            return delay < MaxDelay ? delay : MaxDelay;
        }
    }
}

/// <summary>
/// Manages the lifetime of the CRM real-time hub connection and dispatches strongly typed events to subscribers.
/// </summary>
public sealed class RealtimeHubConnection : IAsyncDisposable
{
    private const int CircuitBreakerThreshold = 3;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(45);

    private readonly IHubConnectionProxyFactory _connectionFactory;
    private readonly IConfiguration _configuration;
    private readonly AuthStateProvider _authStateProvider;
    private readonly CorrelationContext _correlationContext;
    private readonly ILogger<RealtimeHubConnection> _logger;
    private readonly ISnackbar _snackbar;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly List<IDisposable> _registrations = new();
    private readonly List<Action<CustomerCreatedEvent>> _customerCreatedHandlers = new();
    private readonly List<Action<CustomerUpdatedEvent>> _customerUpdatedHandlers = new();
    private readonly List<Action<InvoiceCreatedEvent>> _invoiceCreatedHandlers = new();
    private readonly List<Action<InvoicePaidEvent>> _invoicePaidHandlers = new();
    private readonly List<Action<VehicleAddedEvent>> _vehicleAddedHandlers = new();
    private readonly List<Action<AppointmentScheduledEvent>> _appointmentScheduledHandlers = new();
    private readonly List<Action> _reconnectedHandlers = new();
    private IHubConnectionProxy? _connection;
    private int _consecutiveFailures;
    private DateTimeOffset? _circuitOpenUntil;
    private bool _handlersRegistered;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeHubConnection"/> class.
    /// </summary>
    public RealtimeHubConnection(
        IHubConnectionProxyFactory connectionFactory,
        IConfiguration configuration,
        AuthStateProvider authStateProvider,
        CorrelationContext correlationContext,
        ILogger<RealtimeHubConnection> logger,
        ISnackbar snackbar)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _authStateProvider = authStateProvider ?? throw new ArgumentNullException(nameof(authStateProvider));
        _correlationContext = correlationContext ?? throw new ArgumentNullException(nameof(correlationContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));
    }

    /// <summary>
    /// Ensures the hub connection is active. Any failures trigger circuit breaker behaviour to avoid hot loops.
    /// </summary>
    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_circuitOpenUntil is { } openUntil && openUntil > DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Realtime hub circuit is open until {OpenUntil}.", openUntil);
            return;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectionInitializedAsync(cancellationToken).ConfigureAwait(false);
            if (_connection is null)
            {
                return;
            }

            if (_connection.State == HubConnectionState.Connected)
            {
                return;
            }

            await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
            _consecutiveFailures = 0;
            _circuitOpenUntil = null;
            _logger.LogInformation("Realtime hub connection established.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _consecutiveFailures++;
            _logger.LogWarning(ex, "Failed to start realtime hub connection (attempt {Attempt}).", _consecutiveFailures);

            if (_consecutiveFailures >= CircuitBreakerThreshold)
            {
                _circuitOpenUntil = DateTimeOffset.UtcNow.Add(CircuitOpenDuration);
                _snackbar.Add("CRM live data temporarily unavailable. Retrying soon...", Severity.Warning);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>Registers a handler for the <see cref="CustomerCreatedEvent"/> payload.</summary>
    public IDisposable RegisterCustomerCreated(Action<CustomerCreatedEvent> callback) => RegisterHandler(_customerCreatedHandlers, callback);

    /// <summary>Registers a handler for the <see cref="CustomerUpdatedEvent"/> payload.</summary>
    public IDisposable RegisterCustomerUpdated(Action<CustomerUpdatedEvent> callback) => RegisterHandler(_customerUpdatedHandlers, callback);

    /// <summary>Registers a handler for the <see cref="InvoiceCreatedEvent"/> payload.</summary>
    public IDisposable RegisterInvoiceCreated(Action<InvoiceCreatedEvent> callback) => RegisterHandler(_invoiceCreatedHandlers, callback);

    /// <summary>Registers a handler for the <see cref="InvoicePaidEvent"/> payload.</summary>
    public IDisposable RegisterInvoicePaid(Action<InvoicePaidEvent> callback) => RegisterHandler(_invoicePaidHandlers, callback);

    /// <summary>Registers a handler for the <see cref="VehicleAddedEvent"/> payload.</summary>
    public IDisposable RegisterVehicleAdded(Action<VehicleAddedEvent> callback) => RegisterHandler(_vehicleAddedHandlers, callback);

    /// <summary>Registers a handler for the <see cref="AppointmentScheduledEvent"/> payload.</summary>
    public IDisposable RegisterAppointmentScheduled(Action<AppointmentScheduledEvent> callback) => RegisterHandler(_appointmentScheduledHandlers, callback);

    /// <summary>Registers a handler invoked when the hub reconnects.</summary>
    public IDisposable RegisterReconnected(Action callback) => RegisterHandler(_reconnectedHandlers, callback);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is not null)
            {
                await _connection.StopAsync().ConfigureAwait(false);
                foreach (var registration in _registrations)
                {
                    registration.Dispose();
                }

                _registrations.Clear();
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task EnsureConnectionInitializedAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return;
        }

        var hubUrl = ResolveHubUrl();
        var options = new RealtimeHubConnectionOptions
        {
            HubUrl = hubUrl,
            AccessTokenProvider = _authStateProvider.GetAccessTokenAsync,
        };
        options.Headers[CRMAdapter.Api.Middleware.CorrelationIdMiddleware.CorrelationHeaderName] = _correlationContext.CurrentCorrelationId;

        _connection = _connectionFactory.Create(options);
        RegisterConnectionEvents(_connection);

        if (!_handlersRegistered)
        {
            RegisterHubHandlers(_connection);
            _handlersRegistered = true;
        }

        await Task.CompletedTask;
    }

    private string ResolveHubUrl()
    {
        var configuredUrl = _configuration["Realtime:HubUrl"];
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            return configuredUrl.TrimEnd('/');
        }

        var apiBase = _configuration["Api:BaseUrl"] ?? "https://localhost:5001";
        return $"{apiBase.TrimEnd('/')}/crmhub";
    }

    private void RegisterConnectionEvents(IHubConnectionProxy connection)
    {
        connection.Reconnected += async _ =>
        {
            _logger.LogInformation("Realtime hub connection re-established.");
            _snackbar.Add("Reconnected to CRM Live Data", Severity.Success);
            Notify(_reconnectedHandlers);
            await Task.CompletedTask;
        };

        connection.Reconnecting += async error =>
        {
            if (error is not null)
            {
                _logger.LogWarning(error, "Realtime hub reconnecting due to transient error.");
            }
            else
            {
                _logger.LogInformation("Realtime hub reconnecting.");
            }

            await Task.CompletedTask;
        };

        connection.Closed += async error =>
        {
            if (error is not null)
            {
                _logger.LogWarning(error, "Realtime hub connection closed unexpectedly.");
            }
            else
            {
                _logger.LogInformation("Realtime hub connection closed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            await EnsureConnectedAsync().ConfigureAwait(false);
        };
    }

    private void RegisterHubHandlers(IHubConnectionProxy connection)
    {
        _registrations.Add(connection.On<CustomerCreatedEvent>(nameof(ICrmEventsClient.CustomerCreated), payload => Notify(_customerCreatedHandlers, payload)));
        _registrations.Add(connection.On<CustomerUpdatedEvent>(nameof(ICrmEventsClient.CustomerUpdated), payload => Notify(_customerUpdatedHandlers, payload)));
        _registrations.Add(connection.On<InvoiceCreatedEvent>(nameof(ICrmEventsClient.InvoiceCreated), payload => Notify(_invoiceCreatedHandlers, payload)));
        _registrations.Add(connection.On<InvoicePaidEvent>(nameof(ICrmEventsClient.InvoicePaid), payload => Notify(_invoicePaidHandlers, payload)));
        _registrations.Add(connection.On<VehicleAddedEvent>(nameof(ICrmEventsClient.VehicleAdded), payload => Notify(_vehicleAddedHandlers, payload)));
        _registrations.Add(connection.On<AppointmentScheduledEvent>(nameof(ICrmEventsClient.AppointmentScheduled), payload => Notify(_appointmentScheduledHandlers, payload)));
    }
    private static IDisposable RegisterHandler<T>(ICollection<Action<T>> handlers, Action<T> callback)
    {
        if (handlers is null)
        {
            throw new ArgumentNullException(nameof(handlers));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

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

    private static IDisposable RegisterHandler(ICollection<Action> handlers, Action callback)
    {
        if (handlers is null)
        {
            throw new ArgumentNullException(nameof(handlers));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

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
                // Intentionally swallow to prevent one subscriber from crashing others.
            }
        }
    }

    private static void Notify(ICollection<Action> handlers)
    {
        Action[] snapshot;
        lock (handlers)
        {
            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                handler();
            }
            catch
            {
                // Ignore callback failures to preserve stability.
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public Subscription(Action onDispose)
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _onDispose();
        }
    }
}
