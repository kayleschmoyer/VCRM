// ICustomerDirectory.cs: Abstraction for retrieving customer summaries and detail projections.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Customers.Models;

namespace CRMAdapter.UI.Services.Customers;

public interface ICustomerDirectory
{
    Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default);

    Task<CustomerDetail?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
