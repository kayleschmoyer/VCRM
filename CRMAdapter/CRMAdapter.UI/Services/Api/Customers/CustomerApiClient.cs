// CustomerApiClient.cs: HTTP client for customer endpoints, currently delegating to mock data until live routes are ready.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.Storage;
using CRMAdapter.UI.Core.Sync;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Customers.Models;
using CRMAdapter.UI.Services.Mock.Customers;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Services.Api.Customers;

public sealed class CustomerApiClient : BaseApiClient, ICustomerService
{
    private readonly InMemoryCustomerDirectory _mock;
    private readonly ILocalCache _cache;
    private readonly ISyncQueue _syncQueue;
    private readonly OfflineSyncState _syncState;
    private readonly ILogger<CustomerApiClient> _logger;

    public CustomerApiClient(
        HttpClient client,
        InMemoryCustomerDirectory mock,
        ILocalCache cache,
        ISyncQueue syncQueue,
        OfflineSyncState syncState,
        ILogger<CustomerApiClient> logger)
        : base(client)
    {
        _mock = mock;
        _cache = cache;
        _syncQueue = syncQueue;
        _syncState = syncState;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync("customers", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var customers = await response.Content.ReadFromJsonAsync<List<CustomerSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<CustomerSummary>();
            await CacheSummariesAsync(customers, cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(false);
            return customers;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached customers due to API failure.");
            _syncState.SetOffline(true);
            var cached = await _cache.GetAllAsync<CustomerSummary>(cancellationToken).ConfigureAwait(false);
            if (cached.Count > 0)
            {
                return cached;
            }

            var mock = await _mock.GetCustomersAsync(cancellationToken).ConfigureAwait(false);
            await CacheSummariesAsync(mock.ToList(), cancellationToken).ConfigureAwait(false);
            return mock;
        }
    }

    public async Task<CustomerDetail?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"customers/{customerId}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var detail = await response.Content.ReadFromJsonAsync<CustomerDetail>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (detail is not null)
            {
                await CacheCustomerAsync(detail, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return detail;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached customer {CustomerId} due to API failure.", customerId);
            _syncState.SetOffline(true);
            var cached = await _cache.GetAsync<CustomerDetail>(customerId.ToString(), cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            return await _mock.GetCustomerAsync(customerId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<CustomerDetail> SaveCustomerAsync(CustomerDetail customer, CancellationToken cancellationToken = default)
    {
        if (customer is null)
        {
            throw new ArgumentNullException(nameof(customer));
        }

        try
        {
            var response = await Client.PutAsJsonAsync($"customers/{customer.Id}", customer, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var updated = await response.Content.ReadFromJsonAsync<CustomerDetail>(cancellationToken: cancellationToken).ConfigureAwait(false) ?? customer;
            await CacheCustomerAsync(updated, cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(false);
            return updated;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Queueing customer update for {CustomerId} due to API failure.", customer.Id);
            await _syncQueue.EnqueueChangeAsync(ChangeEnvelope.ForUpdate("Customers", customer.Id.ToString(), customer), cancellationToken).ConfigureAwait(false);
            await CacheCustomerAsync(customer, cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(true);
            return customer;
        }
    }

    private async Task CacheSummariesAsync(IReadOnlyList<CustomerSummary> customers, CancellationToken cancellationToken)
    {
        foreach (var customer in customers)
        {
            await _cache.SetAsync(customer.Id.ToString(), customer, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CacheCustomerAsync(CustomerDetail detail, CancellationToken cancellationToken)
    {
        await _cache.SetAsync(detail.Id.ToString(), detail, cancellationToken).ConfigureAwait(false);
        var summary = new CustomerSummary(
            detail.Id,
            detail.Name,
            detail.Phone,
            detail.Email,
            detail.Vehicles.Count,
            detail.Invoices.OrderByDescending(invoice => invoice.IssuedOn).FirstOrDefault()?.IssuedOn);
        await _cache.SetAsync(summary.Id.ToString(), summary, cancellationToken).ConfigureAwait(false);
    }
}
