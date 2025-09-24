// IInvoiceWorkspace.cs: Contract for exposing invoice summaries, detail, and payment orchestration to the UI.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Invoices.Models;

namespace CRMAdapter.UI.Services.Invoices;

public interface IInvoiceWorkspace
{
    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(string? search = null, CancellationToken cancellationToken = default);

    Task<InvoiceDetail?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<InvoiceDetail?> RecordPaymentAsync(Guid invoiceId, PaymentEntry payment, CancellationToken cancellationToken = default);
}
