/*
 * File: PostalAddress.cs
 * Role: Provides a canonical representation of postal addresses shared by customer and appointment aggregates.
 * Architectural Purpose: Delivers an immutable value object to prevent schema leakage into the CRM layer.
 */
using System;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Canonical immutable postal address for the CRM domain.
    /// </summary>
    public sealed class PostalAddress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PostalAddress"/> class.
        /// </summary>
        /// <param name="line1">Primary street line.</param>
        /// <param name="line2">Secondary street line.</param>
        /// <param name="city">City component.</param>
        /// <param name="stateProvince">State or province.</param>
        /// <param name="postalCode">Postal or ZIP code.</param>
        /// <param name="countryCode">ISO country code.</param>
        public PostalAddress(
            string line1,
            string line2,
            string city,
            string stateProvince,
            string postalCode,
            string countryCode)
        {
            Line1 = line1 ?? throw new ArgumentNullException(nameof(line1));
            Line2 = line2;
            City = city ?? throw new ArgumentNullException(nameof(city));
            StateProvince = stateProvince ?? throw new ArgumentNullException(nameof(stateProvince));
            PostalCode = postalCode ?? throw new ArgumentNullException(nameof(postalCode));
            CountryCode = countryCode ?? throw new ArgumentNullException(nameof(countryCode));
        }

        /// <summary>
        /// Gets the primary street line.
        /// </summary>
        public string Line1 { get; }

        /// <summary>
        /// Gets the secondary street line.
        /// </summary>
        public string Line2 { get; }

        /// <summary>
        /// Gets the city value.
        /// </summary>
        public string City { get; }

        /// <summary>
        /// Gets the state or province value.
        /// </summary>
        public string StateProvince { get; }

        /// <summary>
        /// Gets the postal or ZIP code.
        /// </summary>
        public string PostalCode { get; }

        /// <summary>
        /// Gets the ISO country code.
        /// </summary>
        public string CountryCode { get; }
    }
}
