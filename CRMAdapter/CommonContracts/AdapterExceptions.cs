/*
 * File: AdapterExceptions.cs
 * Purpose: Declares strongly typed adapter exceptions carrying sanitized error codes for enterprise observability.
 * Security Considerations: Ensures consumers receive minimal information while structured logs capture full diagnostics.
 * Example Usage: `throw new CustomerNotFoundException(id);`
 */
using System;
using CRMAdapter.CommonConfig;

namespace CRMAdapter.CommonContracts
{
    /// <summary>
    /// Base class for adapter-specific exceptions with telemetry-friendly error codes.
    /// </summary>
    public abstract class AdapterException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdapterException"/> class.
        /// </summary>
        /// <param name="errorCode">Stable error code for observability.</param>
        /// <param name="message">Sanitized message safe for clients.</param>
        /// <param name="innerException">Optional inner exception.</param>
        protected AdapterException(string errorCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        }

        /// <summary>
        /// Gets the telemetry-friendly error code.
        /// </summary>
        public string ErrorCode { get; }
    }

    /// <summary>
    /// Represents data access failures where the backend could not be reached.
    /// </summary>
    public sealed class AdapterDataAccessException : AdapterException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdapterDataAccessException"/> class.
        /// </summary>
        /// <param name="operation">Name of the failing operation.</param>
        /// <param name="backend">Backend identifier.</param>
        /// <param name="innerException">Original failure.</param>
        public AdapterDataAccessException(string operation, string backend, Exception innerException)
            : base(
                CommonConfig.AdapterErrorCodes.DataAccessFailure,
                $"Operation '{operation}' failed while communicating with backend '{backend}'.",
                innerException)
        {
            Backend = backend;
            Operation = operation;
        }

        /// <summary>
        /// Gets the backend identifier.
        /// </summary>
        public string Backend { get; }

        /// <summary>
        /// Gets the operation identifier.
        /// </summary>
        public string Operation { get; }
    }

    /// <summary>
    /// Represents invalid input supplied to the adapter.
    /// </summary>
    public sealed class InvalidAdapterRequestException : AdapterException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidAdapterRequestException"/> class.
        /// </summary>
        /// <param name="message">Sanitized validation message.</param>
        public InvalidAdapterRequestException(string message)
            : base("VAL001", message)
        {
        }
    }

    /// <summary>
    /// Exception raised when a requested customer was not found.
    /// </summary>
    public sealed class CustomerNotFoundException : AdapterException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomerNotFoundException"/> class.
        /// </summary>
        /// <param name="customerId">Customer identifier.</param>
        public CustomerNotFoundException(Guid customerId)
            : base("CRM404", $"Customer '{customerId}' was not found.")
        {
            CustomerId = customerId;
        }

        /// <summary>
        /// Gets the customer identifier that was not found.
        /// </summary>
        public Guid CustomerId { get; }
    }
}
