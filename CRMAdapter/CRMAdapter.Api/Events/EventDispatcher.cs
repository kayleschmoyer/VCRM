// File: EventDispatcher.cs
// Summary: Static helper used by adapters to broadcast SignalR events to connected clients.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.Api.Hubs;
using CRMAdapter.Api.Middleware;
using CRMAdapter.CommonContracts.Realtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.Api.Events;

/// <summary>
/// Provides a central location for adapters to publish real-time CRM events.
/// </summary>
public static class EventDispatcher
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Configures the dispatcher with the root application service provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve hub dependencies.</param>
    public static void Configure(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>Broadcasts a customer created event.</summary>
    public static Task BroadcastCustomerCreatedAsync(CustomerCreatedEvent payload, CancellationToken cancellationToken = default)
        => BroadcastAsync(client => client.CustomerCreated(payload), nameof(ICrmEventsClient.CustomerCreated), cancellationToken);

    /// <summary>Broadcasts a customer updated event.</summary>
    public static Task BroadcastCustomerUpdatedAsync(CustomerUpdatedEvent payload, CancellationToken cancellationToken = default)
        => BroadcastAsync(client => client.CustomerUpdated(payload), nameof(ICrmEventsClient.CustomerUpdated), cancellationToken);

    /// <summary>Broadcasts an invoice created event.</summary>
    public static Task BroadcastInvoiceCreatedAsync(InvoiceCreatedEvent payload, CancellationToken cancellationToken = default)
        => BroadcastAsync(client => client.InvoiceCreated(payload), nameof(ICrmEventsClient.InvoiceCreated), cancellationToken);

    /// <summary>Broadcasts an invoice paid event.</summary>
    public static Task BroadcastInvoicePaidAsync(InvoicePaidEvent payload, CancellationToken cancellationToken = default)
        => BroadcastAsync(client => client.InvoicePaid(payload), nameof(ICrmEventsClient.InvoicePaid), cancellationToken);

    /// <summary>Broadcasts a vehicle added event.</summary>
    public static Task BroadcastVehicleAddedAsync(VehicleAddedEvent payload, CancellationToken cancellationToken = default)
        => BroadcastAsync(client => client.VehicleAdded(payload), nameof(ICrmEventsClient.VehicleAdded), cancellationToken);

    /// <summary>Broadcasts an appointment scheduled event.</summary>
    public static Task BroadcastAppointmentScheduledAsync(AppointmentScheduledEvent payload, CancellationToken cancellationToken = default)
        => BroadcastAsync(client => client.AppointmentScheduled(payload), nameof(ICrmEventsClient.AppointmentScheduled), cancellationToken);

    private static async Task BroadcastAsync(
        Func<ICrmEventsClient, Task> broadcast,
        string eventName,
        CancellationToken cancellationToken)
    {
        if (broadcast is null)
        {
            throw new ArgumentNullException(nameof(broadcast));
        }

        if (_serviceProvider is null)
        {
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<CrmEventsHub, ICrmEventsClient>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(EventDispatcher));
        var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();

        var correlationId = httpContextAccessor?.HttpContext?.Items?[CorrelationIdMiddleware.CorrelationHeaderName]?.ToString();
        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["EventName"] = eventName,
            ["CorrelationId"] = correlationId,
        }))
        {
            try
            {
                await broadcast(hubContext.Clients.All).ConfigureAwait(false);
                logger.LogInformation("Broadcasted CRM event {EventName} to connected clients.", eventName);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Failed to broadcast CRM event {EventName}.", eventName);
            }
        }
    }
}
