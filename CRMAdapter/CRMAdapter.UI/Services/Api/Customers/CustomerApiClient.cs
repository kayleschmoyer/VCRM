// CustomerApiClient.cs: HTTP client for customer endpoints, currently delegating to mock data until live routes are ready.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Customers.Models;
using CRMAdapter.UI.Services.Mock.Customers;

namespace CRMAdapter.UI.Services.Api.Customers;

public sealed class CustomerApiClient : BaseApiClient, ICustomerService
{
    private readonly InMemoryCustomerDirectory _mock;

    public CustomerApiClient(HttpClient client, InMemoryCustomerDirectory mock)
        : base(client)
    {
        _mock = mock;
    }

    public async Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/customers once backend is wired.
        return await _mock.GetCustomersAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CustomerDetail?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/customers/{customerId} once backend is wired.
        return await _mock.GetCustomerAsync(customerId, cancellationToken).ConfigureAwait(false);
    }
}
