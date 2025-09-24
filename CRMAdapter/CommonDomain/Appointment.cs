/*
 * File: Appointment.cs
 * Purpose: Declares the canonical appointment aggregate bridging scheduling data between backend systems.
 * Security Considerations: Validates identifiers, enforces chronological start/end values, and trims strings to avoid injection payloads.
 * Example Usage: `var appt = new Appointment(id, customerId, vehicleId, startUtc, endUtc, "Advisor", "Confirmed", "Austin");`
 */
using System;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Represents a canonical service appointment within the CRM domain.
    /// </summary>
    public sealed class Appointment
    {
        private const int MaxAdvisorNameLength = 128;
        private const int MaxStatusLength = 64;
        private const int MaxLocationLength = 128;

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
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Appointment id must be non-empty.", nameof(id));
            }

            if (customerId == Guid.Empty)
            {
                throw new ArgumentException("Customer id must be non-empty.", nameof(customerId));
            }

            if (vehicleId == Guid.Empty)
            {
                throw new ArgumentException("Vehicle id must be non-empty.", nameof(vehicleId));
            }

            if (scheduledEnd < scheduledStart)
            {
                throw new ArgumentException("Scheduled end must occur after start.", nameof(scheduledEnd));
            }

            Id = id;
            CustomerId = customerId;
            VehicleId = vehicleId;
            ScheduledStart = scheduledStart;
            ScheduledEnd = scheduledEnd;
            AdvisorName = ValidateRequired(advisorName, nameof(advisorName), MaxAdvisorNameLength);
            Status = ValidateRequired(status, nameof(status), MaxStatusLength);
            Location = ValidateRequired(location, nameof(location), MaxLocationLength);
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

        private static string ValidateRequired(string value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} must be provided.", parameterName);
            }

            var trimmed = value.Trim();
            if (trimmed.Length > maxLength)
            {
                throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
            }

            return trimmed;
        }
    }
}
