// BackgroundSyncWorker.cs: Periodically flushes the offline queue to the CRM API.
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.Storage;
using CRMAdapter.UI.Services.Appointments.Models;
using CRMAdapter.UI.Services.Customers.Models;
using CRMAdapter.UI.Services.Invoices.Models;
using CRMAdapter.UI.Services.Vehicles.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMAdapter.UI.Core.Sync;

public sealed class BackgroundSyncWorker : BackgroundService
{
    private readonly ISyncQueue _syncQueue;
    private readonly IChangeDispatcher _dispatcher;
    private readonly OfflineSyncState _state;
    private readonly OfflineSyncOptions _options;
    private readonly ILogger<BackgroundSyncWorker> _logger;
    private readonly ILocalCache _cache;
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public BackgroundSyncWorker(
        ISyncQueue syncQueue,
        IChangeDispatcher dispatcher,
        OfflineSyncState state,
        ILocalCache cache,
        IOptions<OfflineSyncOptions> options,
        ILogger<BackgroundSyncWorker> logger)
    {
        _syncQueue = syncQueue ?? throw new ArgumentNullException(nameof(syncQueue));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var pending = await _syncQueue.DequeueAllAsync(cancellationToken).ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return;
        }

        _state.SetSyncing(true);
        try
        {
            foreach (var change in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _dispatcher.DispatchAsync(change, cancellationToken).ConfigureAwait(false);
                if (result.Success)
                {
                    await _syncQueue.MarkSyncedAsync(change.CorrelationId, cancellationToken).ConfigureAwait(false);
                    if (result.ServerTimestamp.HasValue)
                    {
                        _state.MarkSuccessfulSync(result.ServerTimestamp.Value);
                    }
                    else
                    {
                        _state.MarkSuccessfulSync(DateTimeOffset.UtcNow);
                    }
                }
                else if (result.Conflict)
                {
                    await _syncQueue.MarkSyncedAsync(change.CorrelationId, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result.ServerPayload))
                    {
                        await ApplyServerPayloadAsync(change, result.ServerPayload!, cancellationToken).ConfigureAwait(false);
                    }
                    _state.ReportConflict(new SyncConflictNotification(change.EntityType, change.EntityId, result.FailureReason ?? "Conflict detected."));
                    _state.MarkSuccessfulSync(result.ServerTimestamp ?? DateTimeOffset.UtcNow);
                }
                else
                {
                    _logger.LogWarning("Stopping sync flush early due to failure: {Reason}", result.FailureReason);
                    break;
                }
            }
        }
        finally
        {
            _state.SetSyncing(false);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Offline sync worker disabled via configuration.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds));
        _logger.LogInformation("Offline sync worker started with interval {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_state.IsOffline)
                {
                    await FlushAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while flushing offline queue.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Offline sync worker stopped.");
    }

    private async Task ApplyServerPayloadAsync(ChangeEnvelope change, string payload, CancellationToken cancellationToken)
    {
        try
        {
            switch (change.EntityType)
            {
                case "Customers":
                    var customer = JsonSerializer.Deserialize<CustomerDetail>(payload, PayloadSerializerOptions);
                    if (customer is not null)
                    {
                        await _cache.SetAsync(customer.Id.ToString(), customer, cancellationToken).ConfigureAwait(false);
                        var summary = new CustomerSummary(
                            customer.Id,
                            customer.Name,
                            customer.Phone,
                            customer.Email,
                            customer.Vehicles.Count,
                            customer.Invoices.OrderByDescending(invoice => invoice.IssuedOn).FirstOrDefault()?.IssuedOn);
                        await _cache.SetAsync(summary.Id.ToString(), summary, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                case "Vehicles":
                    var vehicle = JsonSerializer.Deserialize<VehicleDetail>(payload, PayloadSerializerOptions);
                    if (vehicle is not null)
                    {
                        await _cache.SetAsync(vehicle.Id.ToString(), vehicle, cancellationToken).ConfigureAwait(false);
                        var vehicleSummary = new VehicleSummary(
                            vehicle.Id,
                            vehicle.Vin,
                            vehicle.Year,
                            vehicle.Make,
                            vehicle.Model,
                            vehicle.Owner.Id,
                            vehicle.Owner.Name,
                            vehicle.Plate,
                            vehicle.Status,
                            vehicle.LastServiceDate);
                        await _cache.SetAsync(vehicleSummary.Id.ToString(), vehicleSummary, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                case "Invoices":
                    var invoice = JsonSerializer.Deserialize<InvoiceDetail>(payload, PayloadSerializerOptions);
                    if (invoice is not null)
                    {
                        await _cache.SetAsync(invoice.Id.ToString(), invoice, cancellationToken).ConfigureAwait(false);
                        var invoiceSummary = new InvoiceSummary(
                            invoice.Id,
                            invoice.InvoiceNumber,
                            invoice.Customer.Id,
                            invoice.Customer.Name,
                            invoice.Vehicle.Id,
                            invoice.Vehicle.Vin,
                            invoice.IssuedOn,
                            invoice.Status,
                            invoice.Total,
                            invoice.BalanceDue);
                        await _cache.SetAsync(invoiceSummary.Id.ToString(), invoiceSummary, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                case "Appointments":
                    var appointment = JsonSerializer.Deserialize<AppointmentDetail>(payload, PayloadSerializerOptions);
                    if (appointment is not null)
                    {
                        await _cache.SetAsync(appointment.Id.ToString(), appointment, cancellationToken).ConfigureAwait(false);
                        var appointmentSummary = BuildAppointmentSummary(appointment);
                        await _cache.SetAsync(appointmentSummary.Id.ToString(), appointmentSummary, cancellationToken).ConfigureAwait(false);
                    }
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to hydrate server payload for {EntityType} {EntityId}.", change.EntityType, change.EntityId);
        }
    }

    private static AppointmentSummary BuildAppointmentSummary(AppointmentDetail detail)
    {
        var notesPreview = string.IsNullOrWhiteSpace(detail.Notes)
            ? null
            : detail.Notes.Length > 120
                ? detail.Notes[..120] + "…"
                : detail.Notes;

        return new AppointmentSummary(
            detail.Id,
            detail.AppointmentNumber,
            detail.ScheduledStart,
            detail.ScheduledEnd,
            detail.Status,
            detail.Service,
            detail.Technician,
            detail.Customer,
            detail.Vehicle,
            notesPreview);
    }
}
