// ICustomerService.cs: Stable contract for retrieving customer lists and detail projections across data sources.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Customers.Models;

namespace CRMAdapter.UI.Services.Contracts;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default);

    Task<CustomerDetail?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<CustomerDetail> SaveCustomerAsync(CustomerDetail customer, CancellationToken cancellationToken = default);
}
