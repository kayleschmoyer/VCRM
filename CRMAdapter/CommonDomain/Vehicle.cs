/*
 * File: Vehicle.cs
 * Role: Encapsulates canonical vehicle information shared across CRM adapters.
 * Architectural Purpose: Provides an immutable vehicle aggregate supporting cross-backend normalization
 * and simplifies upstream service integration by abstracting schema divergence.
 */
using System;

namespace CRMAdapter.CommonDomain
{
    /// <summary>
    /// Represents a canonical vehicle entity in the CRM domain.
    /// </summary>
    public sealed class Vehicle
    {
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
            Id = id;
            Vin = vin ?? throw new ArgumentNullException(nameof(vin));
            Make = make ?? throw new ArgumentNullException(nameof(make));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            ModelYear = modelYear;
            OdometerReading = odometerReading;
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
    }

    /// <summary>
    /// Represents a lightweight reference to a vehicle for linking entities together.
    /// </summary>
    public sealed class VehicleReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VehicleReference"/> class.
        /// </summary>
        /// <param name="id">Canonical vehicle identifier.</param>
        /// <param name="vin">Vehicle VIN to display in lists.</param>
        public VehicleReference(Guid id, string vin)
        {
            Id = id;
            Vin = vin ?? throw new ArgumentNullException(nameof(vin));
        }

        /// <summary>
        /// Gets the canonical vehicle identifier.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the normalized VIN.
        /// </summary>
        public string Vin { get; }
    }
}
