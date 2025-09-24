/*
 * File: Customer.cs
 * Purpose: Defines the canonical CRM customer representation, independent from any backend schema.
 * Security Considerations: Performs strict validation of all supplied fields, trims strings, enforces length ceilings, and clones child collections to maintain immutability and prevent tampering.
 * Example Usage: `var customer = new Customer(id, "Ada Lovelace", "ada@example.com", "+15125551212", address, vehicles);`
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Represents the canonical customer aggregate within the CRM domain, decoupled from backend schemas.
    /// </summary>
    public sealed class Customer
    {
        private const int MaxNameLength = 256;
        private const int MaxEmailLength = 256;
        private const int MaxPhoneLength = 32;

        /// <summary>
        /// Initializes a new instance of the <see cref="Customer"/> class.
        /// </summary>
        /// <param name="id">Unique customer identifier in the canonical model.</param>
        /// <param name="displayName">Normalized display name.</param>
        /// <param name="email">Primary contact email.</param>
        /// <param name="primaryPhone">Primary contact phone number.</param>
        /// <param name="postalAddress">Immutable postal address.</param>
        /// <param name="vehicles">Vehicles associated to this customer.</param>
        /// <exception cref="ArgumentException">Thrown when inputs are invalid.</exception>
        public Customer(
            Guid id,
            string displayName,
            string email,
            string primaryPhone,
            PostalAddress postalAddress,
            IReadOnlyCollection<VehicleReference> vehicles)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Customer id must be non-empty.", nameof(id));
            }

            Id = id;
            DisplayName = ValidateRequired(displayName, nameof(displayName), MaxNameLength);
            Email = ValidateEmail(email);
            PrimaryPhone = ValidateRequired(primaryPhone, nameof(primaryPhone), MaxPhoneLength);
            PostalAddress = postalAddress ?? throw new ArgumentNullException(nameof(postalAddress));
            Vehicles = CloneVehicles(vehicles);
        }

        /// <summary>
        /// Gets the canonical identifier of the customer.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the normalized customer name suitable for UI display.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the primary email address.
        /// </summary>
        public string Email { get; }

        /// <summary>
        /// Gets the primary phone number in canonical E.164 format.
        /// </summary>
        public string PrimaryPhone { get; }

        /// <summary>
        /// Gets the immutable postal address for the customer.
        /// </summary>
        public PostalAddress PostalAddress { get; }

        /// <summary>
        /// Gets the vehicles linked to this customer in canonical form.
        /// </summary>
        public IReadOnlyCollection<VehicleReference> Vehicles { get; }

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

        private static string ValidateEmail(string value)
        {
            var email = ValidateRequired(value, nameof(value), MaxEmailLength);
            if (!email.Contains('@', StringComparison.Ordinal))
            {
                throw new ArgumentException("Email must contain '@'.", nameof(value));
            }

            return email;
        }

        private static IReadOnlyCollection<VehicleReference> CloneVehicles(IReadOnlyCollection<VehicleReference> vehicles)
        {
            if (vehicles is null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            var list = vehicles.Where(v => v is not null).ToList();
            if (list.Count != vehicles.Count)
            {
                throw new ArgumentException("Vehicles collection cannot contain null entries.", nameof(vehicles));
            }

            return new ReadOnlyCollection<VehicleReference>(list);
        }
    }
}
