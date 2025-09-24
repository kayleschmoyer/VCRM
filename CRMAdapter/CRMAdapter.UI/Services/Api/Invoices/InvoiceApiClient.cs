// InvoiceApiClient.cs: HTTP client for invoice workflows, temporarily delegating to the mock workspace.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Invoices.Models;
using CRMAdapter.UI.Services.Mock.Invoices;

namespace CRMAdapter.UI.Services.Api.Invoices;

public sealed class InvoiceApiClient : BaseApiClient, IInvoiceService
{
    private readonly InMemoryInvoiceWorkspace _mock;

    public InvoiceApiClient(HttpClient client, InMemoryInvoiceWorkspace mock)
        : base(client)
    {
        _mock = mock;
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/invoices?search=... once backend is wired.
        return await _mock.GetInvoicesAsync(search, cancellationToken).ConfigureAwait(false);
    }

    public async Task<InvoiceDetail?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/invoices/{invoiceId} once backend is wired.
        return await _mock.GetInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/customers/{customerId}/invoices once backend is wired.
        return await _mock.GetInvoicesForCustomerAsync(customerId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/vehicles/{vehicleId}/invoices once backend is wired.
        return await _mock.GetInvoicesForVehicleAsync(vehicleId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<InvoiceDetail?> RecordPaymentAsync(Guid invoiceId, PaymentEntry payment, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with POST /api/invoices/{invoiceId}/payments once backend is wired.
        return await _mock.RecordPaymentAsync(invoiceId, payment, cancellationToken).ConfigureAwait(false);
    }
}
