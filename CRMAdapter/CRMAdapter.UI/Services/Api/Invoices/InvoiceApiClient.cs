// InvoiceApiClient.cs: HTTP client for invoice workflows, temporarily delegating to the mock workspace.
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
using CRMAdapter.UI.Services.Invoices.Models;
using CRMAdapter.UI.Services.Mock.Invoices;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Services.Api.Invoices;

public sealed class InvoiceApiClient : BaseApiClient, IInvoiceService
{
    private readonly InMemoryInvoiceWorkspace _mock;
    private readonly ILocalCache _cache;
    private readonly ISyncQueue _syncQueue;
    private readonly OfflineSyncState _syncState;
    private readonly ILogger<InvoiceApiClient> _logger;

    public InvoiceApiClient(
        HttpClient client,
        InMemoryInvoiceWorkspace mock,
        ILocalCache cache,
        ISyncQueue syncQueue,
        OfflineSyncState syncState,
        ILogger<InvoiceApiClient> logger)
        : base(client)
    {
        _mock = mock;
        _cache = cache;
        _syncQueue = syncQueue;
        _syncState = syncState;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = string.IsNullOrWhiteSpace(search) ? string.Empty : $"?search={Uri.EscapeDataString(search)}";
            var response = await Client.GetAsync($"invoices{query}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var invoices = await response.Content.ReadFromJsonAsync<List<InvoiceSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<InvoiceSummary>();
            foreach (var invoice in invoices)
            {
                await _cache.SetAsync(invoice.Id.ToString(), invoice, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return invoices;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached invoices due to API failure.");
            _syncState.SetOffline(true);
            var cached = await _cache.GetAllAsync<InvoiceSummary>(cancellationToken).ConfigureAwait(false);
            return FilterInvoices(cached, search);
        }
    }

    public async Task<InvoiceDetail?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"invoices/{invoiceId}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var detail = await response.Content.ReadFromJsonAsync<InvoiceDetail>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (detail is not null)
            {
                await CacheInvoiceAsync(detail, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return detail;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached invoice {InvoiceId} due to API failure.", invoiceId);
            _syncState.SetOffline(true);
            var cached = await _cache.GetAsync<InvoiceDetail>(invoiceId.ToString(), cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            return await _mock.GetInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"customers/{customerId}/invoices", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var invoices = await response.Content.ReadFromJsonAsync<List<InvoiceSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<InvoiceSummary>();
            foreach (var invoice in invoices)
            {
                await _cache.SetAsync(invoice.Id.ToString(), invoice, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return invoices;
        }
        catch (HttpRequestException)
        {
            var cached = await _cache.GetAllAsync<InvoiceSummary>(cancellationToken).ConfigureAwait(false);
            return cached.Where(invoice => invoice.CustomerId == customerId).ToList();
        }
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"vehicles/{vehicleId}/invoices", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var invoices = await response.Content.ReadFromJsonAsync<List<InvoiceSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<InvoiceSummary>();
            foreach (var invoice in invoices)
            {
                await _cache.SetAsync(invoice.Id.ToString(), invoice, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return invoices;
        }
        catch (HttpRequestException)
        {
            var cached = await _cache.GetAllAsync<InvoiceSummary>(cancellationToken).ConfigureAwait(false);
            return cached.Where(invoice => invoice.VehicleId == vehicleId).ToList();
        }
    }

    public async Task<InvoiceDetail?> RecordPaymentAsync(Guid invoiceId, PaymentEntry payment, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.PostAsJsonAsync($"invoices/{invoiceId}/payments", payment, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var detail = await response.Content.ReadFromJsonAsync<InvoiceDetail>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (detail is not null)
            {
                await CacheInvoiceAsync(detail, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return detail;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Queueing payment for invoice {InvoiceId} due to API failure.", invoiceId);
            await _syncQueue.EnqueueChangeAsync(ChangeEnvelope.ForUpdate("Invoices", invoiceId.ToString(), new { Payment = payment }), cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(true);
            var cached = await _cache.GetAsync<InvoiceDetail>(invoiceId.ToString(), cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                var updated = cached with
                {
                    PaymentsApplied = cached.PaymentsApplied + payment.Amount,
                    BalanceDue = Math.Max(0, cached.BalanceDue - payment.Amount)
                };
                await CacheInvoiceAsync(updated, cancellationToken).ConfigureAwait(false);
                return updated;
            }

            return await _mock.RecordPaymentAsync(invoiceId, payment, cancellationToken).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<InvoiceSummary> FilterInvoices(IReadOnlyList<InvoiceSummary> invoices, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return invoices;
        }

        return invoices
            .Where(invoice => invoice.InvoiceNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || invoice.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || invoice.VehicleVin.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task CacheInvoiceAsync(InvoiceDetail detail, CancellationToken cancellationToken)
    {
        await _cache.SetAsync(detail.Id.ToString(), detail, cancellationToken).ConfigureAwait(false);
        var summary = new InvoiceSummary(
            detail.Id,
            detail.InvoiceNumber,
            detail.Customer.Id,
            detail.Customer.Name,
            detail.Vehicle.Id,
            detail.Vehicle.Vin,
            detail.IssuedOn,
            detail.Status,
            detail.Total,
            detail.BalanceDue);
        await _cache.SetAsync(summary.Id.ToString(), summary, cancellationToken).ConfigureAwait(false);
    }
}
