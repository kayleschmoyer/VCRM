/*
 * File: AdapterFactory.cs
 * Purpose: Centralizes creation of backend-specific adapters based on configuration.
 * Security Considerations: Ensures configuration files exist, injects retry/logging/throttling dependencies, and prevents adapters from starting with invalid mappings or unsafe connection handling.
 * Example Usage: `var bundle = AdapterFactory.CreateFromEnvironment(() => new SqlConnection(connString), AdapterFactoryOptions.SecureDefaults(logger));`
 */
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using CRMAdapter.CommonConfig;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CRMAdapter.Factory
{
    /// <summary>
    /// Provides configuration for adapter creation including resilience dependencies.
    /// </summary>
    public sealed class AdapterFactoryOptions
    {
        private ISqlRetryPolicy _retryPolicy = new ExponentialBackoffRetryPolicy();
        private IAdapterLogger _logger = NullAdapterLogger.Instance;
        private IAdapterRateLimiter _rateLimiter = NoopAdapterRateLimiter.Instance;

        /// <summary>
        /// Gets or sets the retry policy used by adapters.
        /// </summary>
        public ISqlRetryPolicy RetryPolicy
        {
            get => _retryPolicy;
            set => _retryPolicy = value ?? throw new ArgumentNullException(nameof(RetryPolicy));
        }

        /// <summary>
        /// Gets or sets the structured logger used by adapters.
        /// </summary>
        public IAdapterLogger Logger
        {
            get => _logger;
            set => _logger = value ?? throw new ArgumentNullException(nameof(Logger));
        }

        /// <summary>
        /// Gets or sets the rate limiter used by adapters.
        /// </summary>
        public IAdapterRateLimiter RateLimiter
        {
            get => _rateLimiter;
            set => _rateLimiter = value ?? throw new ArgumentNullException(nameof(RateLimiter));
        }

        /// <summary>
        /// Creates an options instance using the supplied logger and default retry/rate limiting implementations.
        /// </summary>
        /// <param name="logger">Logger to use for adapters.</param>
        /// <param name="maxConcurrency">Maximum number of concurrent adapter operations permitted per rate limiter.</param>
        /// <returns>An options instance.</returns>
        public static AdapterFactoryOptions SecureDefaults(IAdapterLogger? logger = null, int maxConcurrency = 16)
        {
            if (maxConcurrency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Concurrency limit must be positive.");
            }

            var resolvedLogger = logger ?? NullAdapterLogger.Instance;
            return new AdapterFactoryOptions
            {
                Logger = resolvedLogger,
                RetryPolicy = new ExponentialBackoffRetryPolicy(logger: resolvedLogger),
                RateLimiter = new SemaphoreAdapterRateLimiter(maxConcurrency)
            };
        }
    }

    /// <summary>
    /// Creates adapter bundles for a selected backend.
    /// </summary>
    public static class AdapterFactory
    {
        private const string DesktopKey = "VAST_DESKTOP";
        private const string OnlineKey = "VAST_ONLINE";

        /// <summary>
        /// Resolves adapter implementations based on environment variables.
        /// </summary>
        /// <param name="connectionFactory">Factory used to create database connections.</param>
        /// <param name="options">Adapter factory options.</param>
        /// <returns>An adapter bundle for the configured backend.</returns>
        public static AdapterBundle CreateFromEnvironment(
            Func<DbConnection> connectionFactory,
            AdapterFactoryOptions? options = null)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            var backend = Environment.GetEnvironmentVariable("CRM_BACKEND") ?? DesktopKey;
            var mappingOverrideRaw = Environment.GetEnvironmentVariable("CRM_MAPPING_PATH");
            var mappingOverride = string.IsNullOrWhiteSpace(mappingOverrideRaw) ? null : mappingOverrideRaw;
            return Create(backend, connectionFactory, mappingOverride, options);
        }

        /// <summary>
        /// Creates adapter implementations for the specified backend.
        /// </summary>
        /// <param name="backend">Backend key (e.g. VAST_DESKTOP or VAST_ONLINE).</param>
        /// <param name="connectionFactory">Factory used to create database connections.</param>
        /// <param name="mappingPath">Optional explicit mapping file path.</param>
        /// <param name="options">Adapter factory options.</param>
        /// <returns>An adapter bundle.</returns>
        public static AdapterBundle Create(
            string backend,
            Func<DbConnection> connectionFactory,
            string? mappingPath = null,
            AdapterFactoryOptions? options = null)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            var normalizedBackend = NormalizeBackendKey(backend);
            var mappingFile = ResolveMappingPath(normalizedBackend, mappingPath);
            var fieldMap = FieldMap.LoadFromFile(mappingFile);
            var resolvedOptions = options ?? AdapterFactoryOptions.SecureDefaults();

            return normalizedBackend switch
            {
                DesktopKey => CreateVastDesktopBundle(connectionFactory, fieldMap, resolvedOptions),
                OnlineKey => CreateVastOnlineBundle(connectionFactory, fieldMap, resolvedOptions),
                _ => throw new NotSupportedException($"Backend '{normalizedBackend}' is not supported."),
            };
        }

        /// <summary>
        /// Registers Vast Online adapters for dependency injection in a Blazor Server app.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="mappingPath">Path to the Vast Online mapping file.</param>
        /// <param name="connectionFactory">Factory that creates <see cref="DbConnection"/> instances per scope.</param>
        /// <param name="options">Adapter factory options.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddVastOnlineAdapters(
            this IServiceCollection services,
            string mappingPath,
            Func<IServiceProvider, DbConnection> connectionFactory,
            AdapterFactoryOptions? options = null)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            if (string.IsNullOrWhiteSpace(mappingPath))
            {
                throw new ArgumentException("Mapping path must be provided.", nameof(mappingPath));
            }

            var resolvedOptions = options ?? AdapterFactoryOptions.SecureDefaults();
            var absoluteMappingPath = Path.GetFullPath(mappingPath);
            var fieldMap = FieldMap.LoadFromFile(absoluteMappingPath);
            services.AddSingleton(fieldMap);
            services.AddScoped<ICustomerAdapter>(provider =>
                CreateAdapter(
                    () => connectionFactory(provider),
                    connection => new VastOnline.Adapter.CustomerAdapter(
                        connection,
                        provider.GetRequiredService<FieldMap>(),
                        resolvedOptions.RetryPolicy,
                        resolvedOptions.Logger,
                        resolvedOptions.RateLimiter)));
            services.AddScoped<IVehicleAdapter>(provider =>
                CreateAdapter(
                    () => connectionFactory(provider),
                    connection => new VastOnline.Adapter.VehicleAdapter(
                        connection,
                        provider.GetRequiredService<FieldMap>(),
                        resolvedOptions.RetryPolicy,
                        resolvedOptions.Logger,
                        resolvedOptions.RateLimiter)));
            services.AddScoped<IInvoiceAdapter>(provider =>
                CreateAdapter(
                    () => connectionFactory(provider),
                    connection => new VastOnline.Adapter.InvoiceAdapter(
                        connection,
                        provider.GetRequiredService<FieldMap>(),
                        resolvedOptions.RetryPolicy,
                        resolvedOptions.Logger,
                        resolvedOptions.RateLimiter)));
            services.AddScoped<IAppointmentAdapter>(provider =>
                CreateAdapter(
                    () => connectionFactory(provider),
                    connection => new VastOnline.Adapter.AppointmentAdapter(
                        connection,
                        provider.GetRequiredService<FieldMap>(),
                        resolvedOptions.RetryPolicy,
                        resolvedOptions.Logger,
                        resolvedOptions.RateLimiter)));

            return services;
        }

        /// <summary>
        /// Creates a bundle for VAST Desktop consumption (e.g., VB.NET console or COM wrapper).
        /// </summary>
        /// <param name="connectionFactory">Factory used to create database connections.</param>
        /// <param name="mappingPath">Mapping file path.</param>
        /// <param name="options">Adapter factory options.</param>
        /// <returns>An adapter bundle.</returns>
        public static AdapterBundle CreateVastDesktop(
            Func<DbConnection> connectionFactory,
            string mappingPath,
            AdapterFactoryOptions? options = null)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            if (string.IsNullOrWhiteSpace(mappingPath))
            {
                throw new ArgumentException("Mapping path must be provided.", nameof(mappingPath));
            }

            var fieldMap = FieldMap.LoadFromFile(Path.GetFullPath(mappingPath));
            return CreateVastDesktopBundle(connectionFactory, fieldMap, options ?? AdapterFactoryOptions.SecureDefaults());
        }

        private static AdapterBundle CreateVastDesktopBundle(
            Func<DbConnection> connectionFactory,
            FieldMap fieldMap,
            AdapterFactoryOptions options)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            if (fieldMap is null)
            {
                throw new ArgumentNullException(nameof(fieldMap));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var createdAdapters = new List<IDisposable>();
            try
            {
                var customerAdapter = CreateAdapter(connectionFactory, connection =>
                    new Vast.Adapter.CustomerAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(customerAdapter);

                var vehicleAdapter = CreateAdapter(connectionFactory, connection =>
                    new Vast.Adapter.VehicleAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(vehicleAdapter);

                var invoiceAdapter = CreateAdapter(connectionFactory, connection =>
                    new Vast.Adapter.InvoiceAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(invoiceAdapter);

                var appointmentAdapter = CreateAdapter(connectionFactory, connection =>
                    new Vast.Adapter.AppointmentAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(appointmentAdapter);

                return new AdapterBundle(customerAdapter, vehicleAdapter, invoiceAdapter, appointmentAdapter);
            }
            catch
            {
                foreach (var adapter in createdAdapters)
                {
                    adapter.Dispose();
                }

                throw;
            }
        }

        private static AdapterBundle CreateVastOnlineBundle(
            Func<DbConnection> connectionFactory,
            FieldMap fieldMap,
            AdapterFactoryOptions options)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            if (fieldMap is null)
            {
                throw new ArgumentNullException(nameof(fieldMap));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var createdAdapters = new List<IDisposable>();
            try
            {
                var customerAdapter = CreateAdapter(connectionFactory, connection =>
                    new VastOnline.Adapter.CustomerAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(customerAdapter);

                var vehicleAdapter = CreateAdapter(connectionFactory, connection =>
                    new VastOnline.Adapter.VehicleAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(vehicleAdapter);

                var invoiceAdapter = CreateAdapter(connectionFactory, connection =>
                    new VastOnline.Adapter.InvoiceAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(invoiceAdapter);

                var appointmentAdapter = CreateAdapter(connectionFactory, connection =>
                    new VastOnline.Adapter.AppointmentAdapter(connection, fieldMap, options.RetryPolicy, options.Logger, options.RateLimiter));
                createdAdapters.Add(appointmentAdapter);

                return new AdapterBundle(customerAdapter, vehicleAdapter, invoiceAdapter, appointmentAdapter);
            }
            catch
            {
                foreach (var adapter in createdAdapters)
                {
                    adapter.Dispose();
                }

                throw;
            }
        }

        private static TAdapter CreateAdapter<TAdapter>(
            Func<DbConnection> connectionFactory,
            Func<DbConnection, TAdapter> adapterFactory)
            where TAdapter : class, IDisposable
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            if (adapterFactory is null)
            {
                throw new ArgumentNullException(nameof(adapterFactory));
            }

            var connection = connectionFactory() ?? throw new InvalidOperationException("The connection factory returned null.");

            try
            {
                var adapter = adapterFactory(connection);
                if (adapter is null)
                {
                    connection.Dispose();
                    throw new InvalidOperationException("The adapter factory returned null.");
                }

                return adapter;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static string ResolveMappingPath(string backend, string? mappingPath)
        {
            if (!string.IsNullOrWhiteSpace(mappingPath))
            {
                var fullOverridePath = Path.GetFullPath(mappingPath);
                if (!File.Exists(fullOverridePath))
                {
                    throw new FileNotFoundException($"Mapping file '{fullOverridePath}' was not found.");
                }

                return fullOverridePath;
            }

            var baseDirectory = AppContext.BaseDirectory;
            var relativePath = backend switch
            {
                DesktopKey => Path.Combine(baseDirectory, "CRMAdapter", "Vast", "Mapping", "vast-desktop.json"),
                OnlineKey => Path.Combine(baseDirectory, "CRMAdapter", "VastOnline", "Mapping", "vast-online.json"),
                _ => throw new NotSupportedException($"Backend '{backend}' is not supported."),
            };

            var fullPath = Path.GetFullPath(relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Mapping file '{fullPath}' was not found.");
            }

            return fullPath;
        }

        private static string NormalizeBackendKey(string backend)
        {
            if (string.IsNullOrWhiteSpace(backend))
            {
                return DesktopKey;
            }

            return backend.Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// Represents an immutable bundle of adapters for a specific backend.
    /// </summary>
    public sealed class AdapterBundle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdapterBundle"/> class.
        /// </summary>
        public AdapterBundle(
            ICustomerAdapter customerAdapter,
            IVehicleAdapter vehicleAdapter,
            IInvoiceAdapter invoiceAdapter,
            IAppointmentAdapter appointmentAdapter)
        {
            CustomerAdapter = customerAdapter ?? throw new ArgumentNullException(nameof(customerAdapter));
            VehicleAdapter = vehicleAdapter ?? throw new ArgumentNullException(nameof(vehicleAdapter));
            InvoiceAdapter = invoiceAdapter ?? throw new ArgumentNullException(nameof(invoiceAdapter));
            AppointmentAdapter = appointmentAdapter ?? throw new ArgumentNullException(nameof(appointmentAdapter));
        }

        /// <summary>
        /// Gets the customer adapter.
        /// </summary>
        public ICustomerAdapter CustomerAdapter { get; }

        /// <summary>
        /// Gets the vehicle adapter.
        /// </summary>
        public IVehicleAdapter VehicleAdapter { get; }

        /// <summary>
        /// Gets the invoice adapter.
        /// </summary>
        public IInvoiceAdapter InvoiceAdapter { get; }

        /// <summary>
        /// Gets the appointment adapter.
        /// </summary>
        public IAppointmentAdapter AppointmentAdapter { get; }
    }
}
