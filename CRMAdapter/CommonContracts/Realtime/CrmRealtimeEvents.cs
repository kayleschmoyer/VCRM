// CrmRealtimeEvents.cs: Shared event payload contracts for SignalR real-time broadcasting.
using System;

namespace CRMAdapter.CommonContracts.Realtime;

/// <summary>
/// Represents a newly created customer summary pushed to listening clients.
/// </summary>
/// <param name="Id">Unique identifier of the customer.</param>
/// <param name="Name">Display name for the customer record.</param>
/// <param name="Phone">Primary phone number.</param>
/// <param name="Email">Primary email address.</param>
/// <param name="VehicleCount">Number of associated vehicles.</param>
/// <param name="LastInvoiceDate">Date of the most recent invoice for the customer.</param>
public sealed record CustomerCreatedEvent(
    Guid Id,
    string Name,
    string Phone,
    string Email,
    int VehicleCount,
    DateTime? LastInvoiceDate);

/// <summary>
/// Represents an updated customer snapshot pushed to listening clients.
/// </summary>
/// <param name="Id">Unique identifier of the customer.</param>
/// <param name="Name">Display name for the customer record.</param>
/// <param name="Phone">Primary phone number.</param>
/// <param name="Email">Primary email address.</param>
/// <param name="VehicleCount">Number of associated vehicles.</param>
/// <param name="LastInvoiceDate">Date of the most recent invoice for the customer.</param>
public sealed record CustomerUpdatedEvent(
    Guid Id,
    string Name,
    string Phone,
    string Email,
    int VehicleCount,
    DateTime? LastInvoiceDate);

/// <summary>
/// Represents a newly created invoice broadcast to connected dashboards.
/// </summary>
/// <param name="Id">Unique identifier of the invoice.</param>
/// <param name="InvoiceNumber">Human-friendly invoice number.</param>
/// <param name="CustomerId">Identifier of the associated customer.</param>
/// <param name="CustomerName">Customer name for display.</param>
/// <param name="VehicleId">Identifier of the related vehicle.</param>
/// <param name="VehicleVin">Vehicle VIN for quick reference.</param>
/// <param name="IssuedOn">Issued date/time in UTC.</param>
/// <param name="Status">Current invoice status.</param>
/// <param name="Total">Total invoice amount.</param>
/// <param name="BalanceDue">Outstanding balance remaining.</param>
public sealed record InvoiceCreatedEvent(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehicleVin,
    DateTime IssuedOn,
    string Status,
    decimal Total,
    decimal BalanceDue);

/// <summary>
/// Represents an invoice payment notification payload.
/// </summary>
/// <param name="Id">Identifier of the invoice that was paid.</param>
/// <param name="InvoiceNumber">Display invoice number.</param>
/// <param name="CustomerId">Identifier of the related customer.</param>
/// <param name="CustomerName">Name of the customer associated with the invoice.</param>
/// <param name="AmountPaid">Amount applied to the invoice.</param>
/// <param name="PaidOn">Date and time the payment was recorded in UTC.</param>
public sealed record InvoicePaidEvent(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    decimal AmountPaid,
    DateTime PaidOn);

/// <summary>
/// Represents a newly added vehicle associated with a customer.
/// </summary>
/// <param name="Id">Identifier of the vehicle record.</param>
/// <param name="CustomerId">Identifier of the vehicle owner.</param>
/// <param name="CustomerName">Display name for the owner.</param>
/// <param name="Vin">Vehicle VIN.</param>
/// <param name="Make">Vehicle manufacturer.</param>
/// <param name="Model">Vehicle model.</param>
/// <param name="Year">Model year.</param>
public sealed record VehicleAddedEvent(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string Vin,
    string Make,
    string Model,
    int Year);

/// <summary>
/// Represents a scheduled appointment broadcast to service teams.
/// </summary>
/// <param name="Id">Identifier of the appointment.</param>
/// <param name="CustomerId">Identifier of the customer.</param>
/// <param name="CustomerName">Customer name for the appointment.</param>
/// <param name="ScheduledFor">Scheduled date/time in UTC.</param>
/// <param name="ServiceAdvisor">Name of the assigned service advisor.</param>
/// <param name="Reason">Optional description of the appointment reason.</param>
public sealed record AppointmentScheduledEvent(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    DateTime ScheduledFor,
    string ServiceAdvisor,
    string? Reason);
