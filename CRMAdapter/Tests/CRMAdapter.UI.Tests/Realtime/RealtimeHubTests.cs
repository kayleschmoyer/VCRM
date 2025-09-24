// RealtimeHubTests.cs: Validates realtime connection lifecycle and event dispatch behaviour.
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts.Realtime;
using CRMAdapter.UI.Auth;
using CRMAdapter.UI.Services.Diagnostics;
using CRMAdapter.UI.Services.Realtime;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace CRMAdapter.UI.Tests.Realtime;

public sealed class RealtimeHubTests
{
    [Fact]
    public async Task EnsureConnectedAsync_StartsConnectionAndRegistersHandlers()
    {
        var factory = new FakeHubConnectionProxyFactory();
        var snackbar = new SnackbarService();
        var connection = CreateConnection(factory, snackbar);

        await connection.EnsureConnectedAsync();

        factory.Proxy.Should().NotBeNull();
        factory.Proxy!.StartCount.Should().Be(1);
        factory.Proxy.RegisteredMethods.Should().Contain(nameof(ICrmEventsClient.CustomerCreated));
        factory.Proxy.RegisteredMethods.Should().Contain(nameof(ICrmEventsClient.InvoicePaid));
    }

    [Fact]
    public async Task CustomerCreatedEvent_InvokesRegisteredServiceCallback()
    {
        var factory = new FakeHubConnectionProxyFactory();
        var snackbar = new SnackbarService();
        var connection = CreateConnection(factory, snackbar);
        await connection.EnsureConnectedAsync();

        var service = new CustomerRealtimeService(connection, NullLogger<CustomerRealtimeService>.Instance, snackbar);
        await service.EnsureConnectedAsync();

        var invoked = false;
        using var subscription = service.OnCustomerCreated(_ => invoked = true);

        var payload = new CustomerCreatedEvent(Guid.NewGuid(), "Jane Doe", "555-0000", "jane@example.com", 1, DateTime.UtcNow);
        factory.Proxy!.Raise(nameof(ICrmEventsClient.CustomerCreated), payload);

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task ReconnectedEvent_EmitsSnackbarAndCallback()
    {
        var factory = new FakeHubConnectionProxyFactory();
        var snackbar = new SnackbarService();
        var connection = CreateConnection(factory, snackbar);
        var reconnectedCalled = false;
        using var subscription = connection.RegisterReconnected(() => reconnectedCalled = true);

        await connection.EnsureConnectedAsync();
        await factory.Proxy!.TriggerReconnectedAsync();

        reconnectedCalled.Should().BeTrue();
        snackbar.ShownSnackbars.Should().Contain(snack => snack.Message.Contains("Reconnected to CRM Live Data"));
    }

    private static RealtimeHubConnection CreateConnection(FakeHubConnectionProxyFactory factory, ISnackbar snackbar)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Realtime:HubUrl"] = "https://localhost/crmhub",
            })
            .Build();

        var authProvider = CreateAuthStateProvider();
        var correlation = new CorrelationContext();
        return new RealtimeHubConnection(factory, configuration, authProvider, correlation, NullLogger<RealtimeHubConnection>.Instance, snackbar);
    }

    private static AuthStateProvider CreateAuthStateProvider()
    {
        var provider = (AuthStateProvider)FormatterServices.GetUninitializedObject(typeof(AuthStateProvider));
        typeof(AuthStateProvider).GetField("_sessionStorage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(provider, null);
        typeof(AuthStateProvider).GetField("_jwtAuthProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(provider, null);
        typeof(AuthStateProvider).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(provider, NullLogger<AuthStateProvider>.Instance);
        typeof(AuthStateProvider).GetField("_currentUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(provider, new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()));
        return provider;
    }

    private sealed class FakeHubConnectionProxyFactory : IHubConnectionProxyFactory
    {
        public FakeHubConnectionProxy? Proxy { get; private set; }

        public IHubConnectionProxy Create(RealtimeHubConnectionOptions options)
        {
            Proxy = new FakeHubConnectionProxy();
            return Proxy;
        }
    }

    private sealed class FakeHubConnectionProxy : IHubConnectionProxy
    {
        private readonly Dictionary<string, List<Action<object>>> _handlers = new();

        public int StartCount { get; private set; }

        public IList<string> RegisteredMethods { get; } = new List<string>();

        public HubConnectionState State { get; private set; } = HubConnectionState.Disconnected;

        public event Func<Exception?, Task>? Closed;
        public event Func<Exception?, Task>? Reconnecting;
        public event Func<string?, Task>? Reconnected;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            State = HubConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            State = HubConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public IDisposable On<T>(string methodName, Action<T> handler)
        {
            RegisteredMethods.Add(methodName);
            if (!_handlers.TryGetValue(methodName, out var list))
            {
                list = new List<Action<object>>();
                _handlers[methodName] = list;
            }

            Action<object> wrapped = obj => handler((T)obj);
            list.Add(wrapped);
            return new Subscription(() => list.Remove(wrapped));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Raise<T>(string methodName, T payload)
        {
            if (_handlers.TryGetValue(methodName, out var list))
            {
                foreach (var handler in list.ToArray())
                {
                    handler(payload!);
                }
            }
        }

        public Task TriggerReconnectedAsync()
        {
            return Reconnected is null ? Task.CompletedTask : Reconnected.Invoke("reconnected");
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
