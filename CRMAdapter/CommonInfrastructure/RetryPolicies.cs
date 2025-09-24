/*
 * File: RetryPolicies.cs
 * Purpose: Supplies resilient retry policies with exponential backoff for transient data-store failures.
 * Security Considerations: Prevents brute force escalation by bounding retries and centralizes sanitised logging for exceptions.
 * Example Usage: `await retryPolicy.ExecuteAsync((ct) => command.ExecuteNonQueryAsync(ct), cancellationToken);`
 */
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.CommonInfrastructure
{
    /// <summary>
    /// Defines the contract for executing database calls with resilience policies.
    /// </summary>
    public interface ISqlRetryPolicy
    {
        /// <summary>
        /// Executes the supplied operation under the configured retry strategy.
        /// </summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="operation">Operation to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Operation result.</returns>
        Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);

        /// <summary>
        /// Executes the supplied operation under the configured retry strategy.
        /// </summary>
        /// <param name="operation">Operation to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Determines whether an exception represents a transient backend failure.
    /// </summary>
    public interface ITransientErrorDetector
    {
        /// <summary>
        /// Evaluates the provided exception for retry eligibility.
        /// </summary>
        /// <param name="exception">Exception thrown by the backend.</param>
        /// <returns>True when the exception is transient and worth retrying.</returns>
        bool IsTransient(Exception exception);
    }

    /// <summary>
    /// Options controlling retry behaviour.
    /// </summary>
    public sealed class SqlRetryOptions
    {
        private int _maxRetryCount = 3;
        private TimeSpan _baseDelay = TimeSpan.FromMilliseconds(200);
        private TimeSpan _maxDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the maximum number of retries. Default is 3.
        /// </summary>
        public int MaxRetryCount
        {
            get => _maxRetryCount;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "MaxRetryCount cannot be negative.");
                }

                _maxRetryCount = value;
            }
        }

        /// <summary>
        /// Gets or sets the base delay applied before the first retry. Default is 00:00:00.200.
        /// </summary>
        public TimeSpan BaseDelay
        {
            get => _baseDelay;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "BaseDelay must be greater than zero.");
                }

                if (value > _maxDelay)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "BaseDelay cannot exceed MaxDelay ({0}).",
                            _maxDelay));
                }

                _baseDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum backoff delay. Default is 00:00:05.
        /// </summary>
        public TimeSpan MaxDelay
        {
            get => _maxDelay;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "MaxDelay must be greater than zero.");
                }

                if (value < _baseDelay)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "MaxDelay cannot be less than BaseDelay ({0}).",
                            _baseDelay));
                }

                _maxDelay = value;
            }
        }
    }

    /// <summary>
    /// Implements exponential backoff retrying with jitter for SQL operations.
    /// </summary>
    public sealed class ExponentialBackoffRetryPolicy : ISqlRetryPolicy
    {
        private readonly SqlRetryOptions _options;
        private readonly ITransientErrorDetector _transientErrorDetector;
        private readonly IAdapterLogger _logger;
        private readonly Random _jitter = new();
        private readonly object _syncRoot = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ExponentialBackoffRetryPolicy"/> class.
        /// </summary>
        /// <param name="options">Retry configuration.</param>
        /// <param name="transientErrorDetector">Transient error detector.</param>
        /// <param name="logger">Logger used for diagnostics.</param>
        public ExponentialBackoffRetryPolicy(
            SqlRetryOptions? options = null,
            ITransientErrorDetector? transientErrorDetector = null,
            IAdapterLogger? logger = null)
        {
            _options = options ?? new SqlRetryOptions();
            _transientErrorDetector = transientErrorDetector ?? new SqlTransientErrorDetector();
            _logger = logger ?? NullAdapterLogger.Instance;
        }

        /// <inheritdoc />
        public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            return await ExecuteWithRetriesAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            await ExecuteWithRetriesAsync(async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return true;
            }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<TResult> ExecuteWithRetriesAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
        {
            var attempt = 0;
            Exception? lastException = null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await operation(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (_transientErrorDetector.IsTransient(ex) && attempt < _options.MaxRetryCount)
                {
                    lastException = ex;
                    attempt++;
                    var backoff = CalculateDelay(attempt);
                    _logger.LogWarning(
                        "Transient backend failure detected. Retrying with backoff.",
                        new Dictionary<string, object?>
                        {
                            ["Attempt"] = attempt,
                            ["Delay"] = backoff,
                            ["ExceptionType"] = ex.GetType().FullName,
                        });

                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    throw;
                }

                if (attempt >= _options.MaxRetryCount)
                {
                    break;
                }
            }

            throw lastException ?? new InvalidOperationException("Retry policy terminated without executing the operation.");
        }

        private TimeSpan CalculateDelay(int attempt)
        {
            var exponential = Math.Min(_options.MaxDelay.TotalMilliseconds, _options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
            double jitter;
            lock (_syncRoot)
            {
                jitter = _jitter.NextDouble() * _options.BaseDelay.TotalMilliseconds;
            }

            return TimeSpan.FromMilliseconds(Math.Max(0, exponential + jitter));
        }
    }

    /// <summary>
    /// Default transient SQL Server error detector.
    /// </summary>
    public sealed class SqlTransientErrorDetector : ITransientErrorDetector
    {
        private static readonly int[] SqlTransientErrorNumbers =
        {
            4060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920
        };

        /// <inheritdoc />
        public bool IsTransient(Exception exception)
        {
            if (exception is null)
            {
                return false;
            }

            if (exception is TimeoutException)
            {
                return true;
            }

            if (exception is DbException)
            {
                var sqlExceptionType = exception.GetType();
                if (string.Equals(sqlExceptionType.Name, "SqlException", StringComparison.Ordinal))
                {
                    var numberProperty = sqlExceptionType.GetProperty("Number");
                    if (numberProperty is not null)
                    {
                        if (numberProperty.GetValue(exception) is int number && SqlTransientErrorNumbers.Contains(number))
                        {
                            return true;
                        }
                    }

                    var errorsProperty = sqlExceptionType.GetProperty("Errors");
                    if (errorsProperty?.GetValue(exception) is System.Collections.IEnumerable errors)
                    {
                        foreach (var error in errors)
                        {
                            var property = error?.GetType().GetProperty("Number");
                            if (property?.GetValue(error) is int nestedNumber && SqlTransientErrorNumbers.Contains(nestedNumber))
                            {
                                return true;
                            }
                        }
                    }
                }

                // For other DbException types, treat SQLSTATE 08xxx (connection issues) as transient when available.
                var sqlStateProperty = sqlExceptionType.GetProperty("SqlState") ?? sqlExceptionType.GetProperty("SQLState");
                if (sqlStateProperty?.GetValue(exception) is string sqlState && sqlState.StartsWith("08", StringComparison.Ordinal))
                {
                    return true;
                }

                return false;
            }

            return false;
        }
    }
}
