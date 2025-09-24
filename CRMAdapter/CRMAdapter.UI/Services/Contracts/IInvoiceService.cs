// IInvoiceService.cs: Stable contract for invoice querying and payment operations across data providers.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Invoices.Models;

namespace CRMAdapter.UI.Services.Contracts;

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(string? search = null, CancellationToken cancellationToken = default);

    Task<InvoiceDetail?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<InvoiceDetail?> RecordPaymentAsync(Guid invoiceId, PaymentEntry payment, CancellationToken cancellationToken = default);
}
