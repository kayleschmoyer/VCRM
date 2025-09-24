/*
 * File: SqlAdapterBase.cs
 * Purpose: Provides shared SQL helper functionality for Vast Online adapters.
 * Security Considerations: Enforces dependency-injected retry policies, rate limiting, sanitized command creation, and exception wrapping to avoid leaking sensitive backend information.
 * Example Usage: `await ExecuteDbOperationAsync("VehicleAdapter.GetById", ct => command.ExecuteReaderAsync(ct), cancellationToken);`
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonConfig;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonInfrastructure;

namespace CRMAdapter.VastOnline.Adapter
{
    /// <summary>
    /// Base class providing shared SQL helper methods for adapter implementations.
    /// </summary>
    public abstract class SqlAdapterBase : IDisposable
    {
        private bool _disposed;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlAdapterBase"/> class.
        /// </summary>
        /// <param name="connection">Database connection.</param>
        /// <param name="fieldMap">Schema field mapping.</param>
        /// <param name="retryPolicy">Retry policy for transient errors.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="rateLimiter">Rate limiter for throttling.</param>
        protected SqlAdapterBase(
            DbConnection connection,
            FieldMap fieldMap,
            ISqlRetryPolicy? retryPolicy = null,
            IAdapterLogger? logger = null,
            IAdapterRateLimiter? rateLimiter = null)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            FieldMap = fieldMap ?? throw new ArgumentNullException(nameof(fieldMap));
            Logger = logger ?? NullAdapterLogger.Instance;
            RateLimiter = rateLimiter ?? NoopAdapterRateLimiter.Instance;
            RetryPolicy = retryPolicy ?? new ExponentialBackoffRetryPolicy(logger: Logger);
        }

        /// <summary>
        /// Gets the database connection used by the adapter.
        /// </summary>
        protected DbConnection Connection { get; }

        /// <summary>
        /// Gets the field map used by the adapter.
        /// </summary>
        protected FieldMap FieldMap { get; }

        /// <summary>
        /// Gets the retry policy used for SQL operations.
        /// </summary>
        protected ISqlRetryPolicy RetryPolicy { get; }

        /// <summary>
        /// Gets the logger used for diagnostics.
        /// </summary>
        protected IAdapterLogger Logger { get; }

        /// <summary>
        /// Gets the rate limiter enforcing adapter throttling.
        /// </summary>
        protected IAdapterRateLimiter RateLimiter { get; }

        /// <summary>
        /// Gets the backend name from the mapping file.
        /// </summary>
        protected string BackendName => FieldMap.BackendName;

        /// <summary>
        /// Creates a command with the provided text after ensuring the connection is open.
        /// </summary>
        /// <param name="commandText">SQL command text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An initialized <see cref="DbCommand"/>.</returns>
        protected async Task<DbCommand> CreateCommandAsync(string commandText, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                throw new ArgumentException("Command text must be provided.", nameof(commandText));
            }

            await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
            var command = Connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandType = CommandType.Text;
            return command;
        }

        /// <summary>
        /// Adds a parameter with the supplied metadata to the command.
        /// </summary>
        /// <param name="command">Command to add the parameter to.</param>
        /// <param name="name">Parameter name.</param>
        /// <param name="value">Parameter value.</param>
        /// <param name="dbType">Optional database type.</param>
        protected static void AddParameter(DbCommand command, string name, object? value, DbType? dbType = null)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Parameter name must be provided.", nameof(name));
            }

            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            if (dbType.HasValue)
            {
                parameter.DbType = dbType.Value;
            }

            command.Parameters.Add(parameter);
        }

        /// <summary>
        /// Executes a database operation with retry, rate limiting, and safe exception wrapping.
        /// </summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="operationName">Operation identifier used for logging.</param>
        /// <param name="operation">Operation callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Operation result.</returns>
        protected Task<TResult> ExecuteDbOperationAsync<TResult>(
            string operationName,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name must be provided.", nameof(operationName));
            }

            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            return RetryPolicy.ExecuteAsync(async ct =>
            {
                var lease = await RateLimiter.AcquireAsync(operationName, ct).ConfigureAwait(false);
                using (lease)
                {
                    try
                    {
                        return await operation(ct).ConfigureAwait(false);
                    }
                    catch (AdapterException)
                    {
                        throw;
                    }
                    catch (MappingConfigurationException)
                    {
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            "Adapter operation failed.",
                            ex,
                            new Dictionary<string, object?>
                            {
                                ["Operation"] = operationName,
                                ["Backend"] = BackendName,
                                ["ExceptionType"] = ex.GetType().FullName
                            });
                        throw new AdapterDataAccessException(operationName, BackendName, ex);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Executes a database operation that returns no result with retry, rate limiting, and safe exception wrapping.
        /// </summary>
        /// <param name="operationName">Operation identifier used for logging.</param>
        /// <param name="operation">Operation callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected Task ExecuteDbOperationAsync(
            string operationName,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            return ExecuteDbOperationAsync<object?>(operationName, async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// Applies a safe upper bound to caller supplied limits.
        /// </summary>
        /// <param name="requested">Requested limit.</param>
        /// <param name="defaultLimit">Default limit enforced by the adapter.</param>
        /// <param name="parameterName">Parameter name for validation.</param>
        /// <returns>Sanitized limit.</returns>
        protected static int EnforceLimit(int requested, int defaultLimit, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("Parameter name must be provided.", nameof(parameterName));
            }

            if (defaultLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultLimit));
            }

            if (requested <= 0)
            {
                return defaultLimit;
            }

            if (requested > defaultLimit)
            {
                throw new InvalidAdapterRequestException($"{parameterName} cannot exceed {defaultLimit}.");
            }

            return requested;
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the underlying connection.
        /// </summary>
        /// <param name="disposing">Indicates whether managed resources should be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Connection.Dispose();
                _connectionLock.Dispose();
            }

            _disposed = true;
        }

        private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
        {
            if (Connection.State == ConnectionState.Open)
            {
                return;
            }

            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Connection.State != ConnectionState.Open)
                {
                    await Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }
    }
}
