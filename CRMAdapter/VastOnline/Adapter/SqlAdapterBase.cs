/*
 * File: SqlAdapterBase.cs
 * Role: Provides shared SQL helper functionality for Vast Online adapters.
 * Architectural Purpose: Centralizes connection management and parameter creation for consistent async data access.
 */
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonConfig;

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
        protected SqlAdapterBase(DbConnection connection, FieldMap fieldMap)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            FieldMap = fieldMap ?? throw new ArgumentNullException(nameof(fieldMap));
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
        /// Creates a command with the provided text after ensuring the connection is open.
        /// </summary>
        /// <param name="commandText">SQL command text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An initialized <see cref="DbCommand"/>.</returns>
        protected async Task<DbCommand> CreateCommandAsync(string commandText, CancellationToken cancellationToken)
        {
            if (commandText is null)
            {
                throw new ArgumentNullException(nameof(commandText));
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

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
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
