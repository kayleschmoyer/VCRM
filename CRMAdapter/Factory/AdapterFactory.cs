/*
 * File: AdapterFactory.cs
 * Role: Centralizes creation of backend-specific adapters based on configuration.
 * Architectural Purpose: Enables runtime selection of desktop or online adapters while honoring mapping configuration.
 */
using System;
using System.Data.Common;
using System.IO;
using CRMAdapter.CommonConfig;
using CRMAdapter.CommonContracts;
using Microsoft.Extensions.DependencyInjection;

namespace CRMAdapter.Factory
{
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
        /// <returns>An adapter bundle for the configured backend.</returns>
        public static AdapterBundle CreateFromEnvironment(Func<DbConnection> connectionFactory)
        {
            var backend = Environment.GetEnvironmentVariable("CRM_BACKEND") ?? DesktopKey;
            var mappingOverride = Environment.GetEnvironmentVariable("CRM_MAPPING_PATH");
            return Create(backend, connectionFactory, mappingOverride);
        }

        /// <summary>
        /// Creates adapter implementations for the specified backend.
        /// </summary>
        /// <param name="backend">Backend key (e.g. VAST_DESKTOP or VAST_ONLINE).</param>
        /// <param name="connectionFactory">Factory used to create database connections.</param>
        /// <param name="mappingPath">Optional explicit mapping file path.</param>
        /// <returns>An adapter bundle.</returns>
        public static AdapterBundle Create(string backend, Func<DbConnection> connectionFactory, string? mappingPath = null)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            var normalizedBackend = (backend ?? DesktopKey).ToUpperInvariant();
            var mappingFile = ResolveMappingPath(normalizedBackend, mappingPath);
            var fieldMap = FieldMap.LoadFromFile(mappingFile);

            return normalizedBackend switch
            {
                DesktopKey => CreateVastDesktopBundle(connectionFactory, fieldMap),
                OnlineKey => CreateVastOnlineBundle(connectionFactory, fieldMap),
                _ => throw new NotSupportedException($"Backend '{backend}' is not supported."),
            };
        }

        /// <summary>
        /// Registers Vast Online adapters for dependency injection in a Blazor Server app.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="mappingPath">Path to the Vast Online mapping file.</param>
        /// <param name="connectionFactory">Factory that creates <see cref="DbConnection"/> instances per scope.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddVastOnlineAdapters(
            this IServiceCollection services,
            string mappingPath,
            Func<IServiceProvider, DbConnection> connectionFactory)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton(FieldMap.LoadFromFile(mappingPath));
            services.AddScoped<ICustomerAdapter>(provider =>
                new VastOnline.Adapter.CustomerAdapter(connectionFactory(provider), provider.GetRequiredService<FieldMap>()));
            services.AddScoped<IVehicleAdapter>(provider =>
                new VastOnline.Adapter.VehicleAdapter(connectionFactory(provider), provider.GetRequiredService<FieldMap>()));
            services.AddScoped<IInvoiceAdapter>(provider =>
                new VastOnline.Adapter.InvoiceAdapter(connectionFactory(provider), provider.GetRequiredService<FieldMap>()));
            services.AddScoped<IAppointmentAdapter>(provider =>
                new VastOnline.Adapter.AppointmentAdapter(connectionFactory(provider), provider.GetRequiredService<FieldMap>()));

            return services;
        }

        /// <summary>
        /// Creates a bundle for VAST Desktop consumption (e.g., VB.NET console or COM wrapper).
        /// </summary>
        /// <param name="connectionFactory">Factory used to create database connections.</param>
        /// <param name="mappingPath">Mapping file path.</param>
        /// <returns>An adapter bundle.</returns>
        public static AdapterBundle CreateVastDesktop(Func<DbConnection> connectionFactory, string mappingPath)
        {
            var fieldMap = FieldMap.LoadFromFile(mappingPath);
            return CreateVastDesktopBundle(connectionFactory, fieldMap);
        }

        private static AdapterBundle CreateVastDesktopBundle(Func<DbConnection> connectionFactory, FieldMap fieldMap)
        {
            return new AdapterBundle(
                new Vast.Adapter.CustomerAdapter(connectionFactory(), fieldMap),
                new Vast.Adapter.VehicleAdapter(connectionFactory(), fieldMap),
                new Vast.Adapter.InvoiceAdapter(connectionFactory(), fieldMap),
                new Vast.Adapter.AppointmentAdapter(connectionFactory(), fieldMap));
        }

        private static AdapterBundle CreateVastOnlineBundle(Func<DbConnection> connectionFactory, FieldMap fieldMap)
        {
            return new AdapterBundle(
                new VastOnline.Adapter.CustomerAdapter(connectionFactory(), fieldMap),
                new VastOnline.Adapter.VehicleAdapter(connectionFactory(), fieldMap),
                new VastOnline.Adapter.InvoiceAdapter(connectionFactory(), fieldMap),
                new VastOnline.Adapter.AppointmentAdapter(connectionFactory(), fieldMap));
        }

        private static string ResolveMappingPath(string backend, string? mappingPath)
        {
            if (!string.IsNullOrWhiteSpace(mappingPath))
            {
                return mappingPath!;
            }

            var baseDirectory = AppContext.BaseDirectory;
            var relativePath = backend switch
            {
                DesktopKey => Path.Combine(baseDirectory, "CRMAdapter", "Vast", "Mapping", "vast-desktop.json"),
                OnlineKey => Path.Combine(baseDirectory, "CRMAdapter", "VastOnline", "Mapping", "vast-online.json"),
                _ => throw new NotSupportedException($"Backend '{backend}' is not supported."),
            };

            return relativePath;
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
