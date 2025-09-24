/*
 * File: Customer.cs
 * Role: Defines the canonical CRM customer representation, independent from any backend schema.
 * Architectural Purpose: Provides an immutable, domain-rich object that acts as the single
 * source of truth for customer data flowing through the adapter framework.
 */
using System;
using System.Collections.Generic;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Represents the canonical customer aggregate within the CRM domain, decoupled from backend schemas.
    /// </summary>
    public sealed class Customer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Customer"/> class.
        /// </summary>
        /// <param name="id">Unique customer identifier in the canonical model.</param>
        /// <param name="displayName">Normalized display name.</param>
        /// <param name="email">Primary contact email.</param>
        /// <param name="primaryPhone">Primary contact phone number.</param>
        /// <param name="postalAddress">Immutable postal address.</param>
        /// <param name="vehicles">Vehicles associated to this customer.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required argument is null.</exception>
        public Customer(
            Guid id,
            string displayName,
            string email,
            string primaryPhone,
            PostalAddress postalAddress,
            IReadOnlyCollection<VehicleReference> vehicles)
        {
            Id = id;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Email = email ?? throw new ArgumentNullException(nameof(email));
            PrimaryPhone = primaryPhone ?? throw new ArgumentNullException(nameof(primaryPhone));
            PostalAddress = postalAddress ?? throw new ArgumentNullException(nameof(postalAddress));
            Vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
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
    }
}
