/*
 * File: Vehicle.cs
 * Purpose: Encapsulates canonical vehicle information shared across CRM adapters.
 * Security Considerations: Validates all fields, enforces VIN length boundaries, and protects against negative mileage injection.
 * Example Usage: `var vehicle = new Vehicle(id, "1FTSW21R08EB53158", "Ford", "F-150", 2023, 12000, customerId);`
 */
using System;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Represents a canonical vehicle entity in the CRM domain.
    /// </summary>
    public sealed class Vehicle
    {
        private const int MaxVinLength = 32;
        private const int MaxMakeLength = 64;
        private const int MaxModelLength = 64;
        private const int MinModelYear = 1900;
        private const int MaxModelYear = 2100;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vehicle"/> class.
        /// </summary>
        /// <param name="id">Canonical vehicle identifier.</param>
        /// <param name="vin">Vehicle identification number.</param>
        /// <param name="make">Vehicle manufacturer name.</param>
        /// <param name="model">Vehicle model designation.</param>
        /// <param name="modelYear">Four digit model year.</param>
        /// <param name="odometerReading">Latest known odometer reading in miles.</param>
        /// <param name="customerId">Identifier linking to the owning customer.</param>
        public Vehicle(
            Guid id,
            string vin,
            string make,
            string model,
            int modelYear,
            int odometerReading,
            Guid customerId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Vehicle id must be non-empty.", nameof(id));
            }

            if (customerId == Guid.Empty)
            {
                throw new ArgumentException("Customer id must be non-empty.", nameof(customerId));
            }

            Id = id;
            Vin = ValidateRequired(vin, nameof(vin), MaxVinLength);
            Make = ValidateRequired(make, nameof(make), MaxMakeLength);
            Model = ValidateRequired(model, nameof(model), MaxModelLength);
            ModelYear = ValidateModelYear(modelYear);
            OdometerReading = ValidateOdometer(odometerReading);
            CustomerId = customerId;
        }

        /// <summary>
        /// Gets the canonical vehicle identifier.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the normalized vehicle identification number.
        /// </summary>
        public string Vin { get; }

        /// <summary>
        /// Gets the vehicle make.
        /// </summary>
        public string Make { get; }

        /// <summary>
        /// Gets the vehicle model.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Gets the vehicle model year.
        /// </summary>
        public int ModelYear { get; }

        /// <summary>
        /// Gets the most recent odometer reading in miles.
        /// </summary>
        public int OdometerReading { get; }

        /// <summary>
        /// Gets the owning customer's canonical identifier.
        /// </summary>
        public Guid CustomerId { get; }

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

        private static int ValidateModelYear(int value)
        {
            if (value < MinModelYear || value > MaxModelYear)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Model year must be between {MinModelYear} and {MaxModelYear}.");
            }

            return value;
        }

        private static int ValidateOdometer(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Odometer reading cannot be negative.");
            }

            return value;
        }
    }

    /// <summary>
    /// Represents a lightweight reference to a vehicle for linking entities together.
    /// </summary>
    public sealed class VehicleReference
    {
        private const int MaxVinLength = 32;

        /// <summary>
        /// Initializes a new instance of the <see cref="VehicleReference"/> class.
        /// </summary>
        /// <param name="id">Canonical vehicle identifier.</param>
        /// <param name="vin">Vehicle VIN to display in lists.</param>
        public VehicleReference(Guid id, string vin)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Vehicle id must be non-empty.", nameof(id));
            }

            Id = id;
            Vin = ValidateVin(vin);
        }

        /// <summary>
        /// Gets the canonical vehicle identifier.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the normalized VIN.
        /// </summary>
        public string Vin { get; }

        private static string ValidateVin(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin))
            {
                throw new ArgumentException("VIN must be provided.", nameof(vin));
            }

            var trimmed = vin.Trim();
            if (trimmed.Length > MaxVinLength)
            {
                throw new ArgumentException($"VIN cannot exceed {MaxVinLength} characters.", nameof(vin));
            }

            return trimmed;
        }
    }
}
