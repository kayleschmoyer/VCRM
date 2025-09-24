// File: CrmEventsHub.cs
// Summary: SignalR hub that broadcasts CRM domain events to authenticated clients.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRMAdapter.Api.Middleware;
using CRMAdapter.CommonContracts.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.Api.Hubs;

/// <summary>
/// Defines strongly typed client methods that the hub can invoke.
/// </summary>
public interface ICrmEventsClient
{
    /// <summary>Broadcasts that a customer was created.</summary>
    /// <param name="payload">Customer payload pushed to clients.</param>
    Task CustomerCreated(CustomerCreatedEvent payload);

    /// <summary>Broadcasts that a customer was updated.</summary>
    /// <param name="payload">Customer payload pushed to clients.</param>
    Task CustomerUpdated(CustomerUpdatedEvent payload);

    /// <summary>Broadcasts that an invoice was created.</summary>
    /// <param name="payload">Invoice payload pushed to clients.</param>
    Task InvoiceCreated(InvoiceCreatedEvent payload);

    /// <summary>Broadcasts that an invoice was paid.</summary>
    /// <param name="payload">Invoice payment payload.</param>
    Task InvoicePaid(InvoicePaidEvent payload);

    /// <summary>Broadcasts that a vehicle was added.</summary>
    /// <param name="payload">Vehicle payload pushed to clients.</param>
    Task VehicleAdded(VehicleAddedEvent payload);

    /// <summary>Broadcasts that an appointment was scheduled.</summary>
    /// <param name="payload">Appointment payload pushed to clients.</param>
    Task AppointmentScheduled(AppointmentScheduledEvent payload);
}

/// <summary>
/// Hub used to publish CRM domain events to connected Blazor clients in real time.
/// </summary>
[Authorize(Roles = "Admin,Manager,Clerk,Tech")]
public sealed class CrmEventsHub : Hub<ICrmEventsClient>
{
    private readonly ILogger<CrmEventsHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmEventsHub"/> class.
    /// </summary>
    /// <param name="logger">Logger used to capture connection lifecycle telemetry.</param>
    public CrmEventsHub(ILogger<CrmEventsHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var correlationId = httpContext?.Request.Headers[CorrelationIdMiddleware.CorrelationHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["ConnectionId"] = Context.ConnectionId,
        }))
        {
            _logger.LogInformation("Client {ConnectionId} connected to CRM events hub.", Context.ConnectionId);
        }

        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["ConnectionId"] = Context.ConnectionId,
        }))
        {
            if (exception is null)
            {
                _logger.LogInformation("Client {ConnectionId} disconnected from CRM events hub.", Context.ConnectionId);
            }
            else
            {
                _logger.LogWarning(exception, "Client {ConnectionId} disconnected from CRM events hub due to an error.", Context.ConnectionId);
            }
        }

        return base.OnDisconnectedAsync(exception);
    }
}
