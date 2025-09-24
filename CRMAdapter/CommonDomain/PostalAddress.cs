/*
 * File: PostalAddress.cs
 * Purpose: Provides a canonical representation of postal addresses shared by customer and appointment aggregates.
 * Security Considerations: Enforces strict length validation and trims inputs to eliminate overflows or injection payloads.
 * Example Usage: `var address = new PostalAddress("123 Main", null, "Austin", "TX", "78701", "US");`
 */
using System;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Canonical immutable postal address for the CRM domain.
    /// </summary>
    public sealed class PostalAddress
    {
        private const int MaxLineLength = 256;
        private const int MaxCityLength = 128;
        private const int MaxStateLength = 64;
        private const int MaxPostalCodeLength = 32;
        private const int CountryCodeLength = 2;

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
            string? line2,
            string city,
            string stateProvince,
            string postalCode,
            string countryCode)
        {
            Line1 = ValidateRequired(line1, nameof(line1), MaxLineLength);
            Line2 = ValidateOptional(line2, MaxLineLength);
            City = ValidateRequired(city, nameof(city), MaxCityLength);
            StateProvince = ValidateRequired(stateProvince, nameof(stateProvince), MaxStateLength);
            PostalCode = ValidateRequired(postalCode, nameof(postalCode), MaxPostalCodeLength);
            CountryCode = ValidateCountryCode(countryCode);
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

        private static string ValidateOptional(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            if (trimmed.Length > maxLength)
            {
                throw new ArgumentException($"Optional address line cannot exceed {maxLength} characters.", nameof(value));
            }

            return trimmed;
        }

        private static string ValidateCountryCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Country code must be provided.", nameof(value));
            }

            var trimmed = value.Trim().ToUpperInvariant();
            if (trimmed.Length != CountryCodeLength)
            {
                throw new ArgumentException($"Country code must be {CountryCodeLength} characters.", nameof(value));
            }

            foreach (var c in trimmed)
            {
                if (!char.IsLetter(c))
                {
                    throw new ArgumentException("Country code must contain only letters.", nameof(value));
                }
            }

            return trimmed;
        }
    }
}
