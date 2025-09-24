using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.Storage;
using CRMAdapter.UI.Core.Sync;
using CRMAdapter.UI.Services.Api.Customers;
using CRMAdapter.UI.Services.Customers.Models;
using CRMAdapter.UI.Services.Mock.Customers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMAdapter.UI.Tests.Offline;

public sealed class OfflineCacheTests : IDisposable
{
    private readonly List<string> _paths = new();

    [Fact]
    public async Task Reads_from_cache_when_api_down()
    {
        var cache = CreateCache();
        var state = new OfflineSyncState();
        var handler = new StubHandler(JsonSerializer.Serialize(new List<CustomerSummary>
        {
            new(Guid.NewGuid(), "Test Customer", "+1-555-0100", "test@example.com", 2, DateTime.UtcNow)
        }));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var service = new CustomerApiClient(
            client,
            new InMemoryCustomerDirectory(),
            cache,
            new NoopSyncQueue(),
            state,
            NullLogger<CustomerApiClient>.Instance);

        var initial = await service.GetCustomersAsync();
        initial.Should().ContainSingle().Which.Name.Should().Be("Test Customer");

        var offlineClient = new CustomerApiClient(
            new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://localhost") },
            new InMemoryCustomerDirectory(),
            cache,
            new NoopSyncQueue(),
            state,
            NullLogger<CustomerApiClient>.Instance);

        var offline = await offlineClient.GetCustomersAsync();
        offline.Should().ContainSingle().Which.Name.Should().Be("Test Customer");
    }

    [Fact]
    public async Task Enqueued_changes_replay_when_api_restored()
    {
        var cache = CreateCache();
        var state = new OfflineSyncState();
        var queue = new SyncQueue(cache, state, NullLogger<SyncQueue>.Instance);
        var dispatcher = new ToggleDispatcher();
        var worker = new BackgroundSyncWorker(
            queue,
            dispatcher,
            state,
            cache,
            Options.Create(new OfflineSyncOptions { Enabled = true, IntervalSeconds = 1 }),
            NullLogger<BackgroundSyncWorker>.Instance);

        await queue.EnqueueChangeAsync(ChangeEnvelope.ForUpdate("Customers", Guid.NewGuid().ToString(), new { Name = "Offline" }));

        dispatcher.ShouldFail = true;
        await worker.FlushAsync(CancellationToken.None);
        (await queue.GetLengthAsync()).Should().Be(1);

        dispatcher.ShouldFail = false;
        await worker.FlushAsync(CancellationToken.None);
        (await queue.GetLengthAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Server_wins_on_conflict()
    {
        var cache = CreateCache();
        var state = new OfflineSyncState();
        var queue = new SyncQueue(cache, state, NullLogger<SyncQueue>.Instance);
        var dispatcher = new ConflictDispatcher();
        var worker = new BackgroundSyncWorker(
            queue,
            dispatcher,
            state,
            cache,
            Options.Create(new OfflineSyncOptions { Enabled = true, IntervalSeconds = 1 }),
            NullLogger<BackgroundSyncWorker>.Instance);

        var customerId = Guid.NewGuid();
        await queue.EnqueueChangeAsync(ChangeEnvelope.ForUpdate("Customers", customerId.ToString(), new { Name = "Local" }));

        var conflictRaised = false;
        state.ConflictDetected += _ => conflictRaised = true;

        dispatcher.Payload = JsonSerializer.Serialize(new CustomerDetail(
            customerId,
            "Server Customer",
            "server@example.com",
            "+1-555-4242",
            "Notes",
            Array.Empty<VehicleRecord>(),
            Array.Empty<InvoiceRecord>(),
            Array.Empty<AppointmentRecord>()));

        await worker.FlushAsync(CancellationToken.None);

        conflictRaised.Should().BeTrue();
        (await queue.GetLengthAsync()).Should().Be(0);
        var cachedCustomer = await cache.GetAsync<CustomerDetail>(customerId.ToString());
        cachedCustomer.Should().NotBeNull();
        cachedCustomer!.Name.Should().Be("Server Customer");
    }

    private ILocalCache CreateCache()
    {
        var path = Path.Combine(Path.GetTempPath(), "crm-offline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _paths.Add(path);
        return new FileSystemCache(path);
    }

    public void Dispose()
    {
        foreach (var path in _paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private sealed class NoopSyncQueue : ISyncQueue
    {
        public Task EnqueueChangeAsync(ChangeEnvelope change, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ChangeEnvelope>> DequeueAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChangeEnvelope>>(Array.Empty<ChangeEnvelope>());

        public Task MarkSyncedAsync(Guid correlationId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> GetLengthAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _payload;

        public StubHandler(string payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Offline");
    }

    private sealed class ToggleDispatcher : IChangeDispatcher
    {
        public bool ShouldFail { get; set; }

        public Task<ChangeDispatchResult> DispatchAsync(ChangeEnvelope change, CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                return Task.FromResult(ChangeDispatchResult.Failed("offline"));
            }

            return Task.FromResult(ChangeDispatchResult.Successful(DateTimeOffset.UtcNow));
        }
    }

    private sealed class ConflictDispatcher : IChangeDispatcher
    {
        public string Payload { get; set; } = string.Empty;

        public Task<ChangeDispatchResult> DispatchAsync(ChangeEnvelope change, CancellationToken cancellationToken)
        {
            return Task.FromResult(ChangeDispatchResult.ConflictDetected(DateTimeOffset.UtcNow, Payload, "Conflict"));
        }
    }
}
