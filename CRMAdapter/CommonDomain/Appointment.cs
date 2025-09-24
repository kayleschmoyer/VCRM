/*
 * File: Appointment.cs
 * Role: Declares the canonical appointment aggregate bridging scheduling data between backend systems.
 * Architectural Purpose: Provides a normalized scheduling model for customer engagement workflows.
 */
using System;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Represents a canonical service appointment within the CRM domain.
    /// </summary>
    public sealed class Appointment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Appointment"/> class.
        /// </summary>
        /// <param name="id">Canonical appointment identifier.</param>
        /// <param name="customerId">Identifier of the customer associated with the appointment.</param>
        /// <param name="vehicleId">Identifier of the vehicle serviced during the appointment.</param>
        /// <param name="scheduledStart">Scheduled start date and time.</param>
        /// <param name="scheduledEnd">Scheduled end date and time.</param>
        /// <param name="advisorName">Assigned service advisor.</param>
        /// <param name="status">Canonical appointment status.</param>
        /// <param name="location">Appointment location.</param>
        public Appointment(
            Guid id,
            Guid customerId,
            Guid vehicleId,
            DateTime scheduledStart,
            DateTime scheduledEnd,
            string advisorName,
            string status,
            string location)
        {
            Id = id;
            CustomerId = customerId;
            VehicleId = vehicleId;
            ScheduledStart = scheduledStart;
            ScheduledEnd = scheduledEnd;
            AdvisorName = advisorName ?? throw new ArgumentNullException(nameof(advisorName));
            Status = status ?? throw new ArgumentNullException(nameof(status));
            Location = location ?? throw new ArgumentNullException(nameof(location));
        }

        /// <summary>
        /// Gets the canonical appointment identifier.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the customer identifier.
        /// </summary>
        public Guid CustomerId { get; }

        /// <summary>
        /// Gets the vehicle identifier.
        /// </summary>
        public Guid VehicleId { get; }

        /// <summary>
        /// Gets the scheduled start time in UTC.
        /// </summary>
        public DateTime ScheduledStart { get; }

        /// <summary>
        /// Gets the scheduled end time in UTC.
        /// </summary>
        public DateTime ScheduledEnd { get; }

        /// <summary>
        /// Gets the assigned service advisor name.
        /// </summary>
        public string AdvisorName { get; }

        /// <summary>
        /// Gets the canonical appointment status.
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets the location for the appointment.
        /// </summary>
        public string Location { get; }
    }
}
