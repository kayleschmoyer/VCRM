// DataSourceStrategy.cs: Central hybrid resolver that maps domain contracts to mock or live services.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using CRMAdapter.UI.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMAdapter.UI.Core.DataSource;

/// <summary>
/// Resolves domain services based on configuration and runtime overrides, supporting mock/live/auto modes.
/// </summary>
public sealed class DataSourceStrategy : IDataSourceStrategy
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataSourceStrategy> _logger;
    private readonly DataSourceOptions _options;
    private readonly IReadOnlyDictionary<Type, DataSourceRegistration> _registrations;
    private readonly IReadOnlyDictionary<string, DataSourceRegistration> _registrationsByKey;
    private readonly ConcurrentDictionary<Type, DataSourceMode> _overrides = new();

    public DataSourceStrategy(
        IServiceProvider serviceProvider,
        IOptions<DataSourceOptions> options,
        ILogger<DataSourceStrategy> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;

        _registrations = BuildRegistrations();
        _registrationsByKey = new Dictionary<string, DataSourceRegistration>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in _registrations.Values)
        {
            _registrationsByKey[registration.Key] = registration;
        }
    }

    public TService GetService<TService>() where TService : class
    {
        var contractType = typeof(TService);
        if (!_registrations.TryGetValue(contractType, out var registration))
        {
            throw new InvalidOperationException($"No data source registration found for {contractType.Name}.");
        }

        var mode = GetEffectiveMode(contractType, registration);
        return (TService)Resolve(registration, mode);
    }

    public DataSourceMode GetMode<TService>() where TService : class
    {
        var contractType = typeof(TService);
        if (!_registrations.TryGetValue(contractType, out var registration))
        {
            throw new InvalidOperationException($"No data source registration found for {contractType.Name}.");
        }

        return GetEffectiveMode(contractType, registration);
    }

    public IReadOnlyDictionary<string, DataSourceMode> GetConfiguredModes()
    {
        var snapshot = new Dictionary<string, DataSourceMode>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _registrationsByKey)
        {
            var mode = GetEffectiveMode(pair.Value.ContractType, pair.Value);
            snapshot[pair.Key] = mode;
        }

        return snapshot;
    }

    public bool TrySetOverride(string entityKey, DataSourceMode mode)
    {
        if (!_registrationsByKey.TryGetValue(entityKey, out var registration))
        {
            return false;
        }

        _overrides[registration.ContractType] = mode;
        _logger.LogInformation("Data source override applied for {EntityKey}: {Mode}", entityKey, mode);
        return true;
    }

    public void ClearOverride(string entityKey)
    {
        if (_registrationsByKey.TryGetValue(entityKey, out var registration))
        {
            _overrides.TryRemove(registration.ContractType, out _);
            _logger.LogInformation("Data source override cleared for {EntityKey}", entityKey);
        }
    }

    private DataSourceMode GetEffectiveMode(Type contractType, DataSourceRegistration registration)
    {
        if (_overrides.TryGetValue(contractType, out var overrideMode))
        {
            return overrideMode;
        }

        return registration.ModeSelector(_options);
    }

    private object Resolve(DataSourceRegistration registration, DataSourceMode mode)
    {
        return mode switch
        {
            DataSourceMode.Mock => registration.MockFactory(_serviceProvider),
            DataSourceMode.Live => registration.LiveFactory(_serviceProvider),
            DataSourceMode.Auto => CreateHybrid(registration),
            _ => registration.MockFactory(_serviceProvider)
        };
    }

    private object CreateHybrid(DataSourceRegistration registration)
    {
        var live = registration.LiveFactory(_serviceProvider);
        var mock = registration.MockFactory(_serviceProvider);

        var proxyType = typeof(HybridServiceProxy<>).MakeGenericType(registration.ContractType);
        var createMethod = proxyType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        if (createMethod is null)
        {
            throw new InvalidOperationException("Unable to create hybrid proxy for " + registration.ContractType.Name);
        }

        return createMethod.Invoke(null, new[] { live, mock, _logger })!;
    }

    private IReadOnlyDictionary<Type, DataSourceRegistration> BuildRegistrations()
    {
        return new Dictionary<Type, DataSourceRegistration>
        {
            [typeof(ICustomerService)] = new(
                typeof(ICustomerService),
                "Customers",
                options => options.Customers,
                sp => sp.GetRequiredService<Services.Mock.Customers.InMemoryCustomerDirectory>(),
                sp => sp.GetRequiredService<Services.Api.Customers.CustomerApiClient>()),
            [typeof(IVehicleService)] = new(
                typeof(IVehicleService),
                "Vehicles",
                options => options.Vehicles,
                sp => sp.GetRequiredService<Services.Mock.Vehicles.InMemoryVehicleRegistry>(),
                sp => sp.GetRequiredService<Services.Api.Vehicles.VehicleApiClient>()),
            [typeof(IInvoiceService)] = new(
                typeof(IInvoiceService),
                "Invoices",
                options => options.Invoices,
                sp => sp.GetRequiredService<Services.Mock.Invoices.InMemoryInvoiceWorkspace>(),
                sp => sp.GetRequiredService<Services.Api.Invoices.InvoiceApiClient>()),
            [typeof(IAppointmentService)] = new(
                typeof(IAppointmentService),
                "Appointments",
                options => options.Appointments,
                sp => sp.GetRequiredService<Services.Mock.Appointments.InMemoryAppointmentBook>(),
                sp => sp.GetRequiredService<Services.Api.Appointments.AppointmentApiClient>()),
            [typeof(IDashboardService)] = new(
                typeof(IDashboardService),
                "Dashboard",
                options => options.Dashboard,
                sp => sp.GetRequiredService<Services.Mock.Dashboard.InMemoryDashboardAnalytics>(),
                sp => sp.GetRequiredService<Services.Api.Dashboard.DashboardApiClient>())
        };
    }

    private sealed record DataSourceRegistration(
        Type ContractType,
        string Key,
        Func<DataSourceOptions, DataSourceMode> ModeSelector,
        Func<IServiceProvider, object> MockFactory,
        Func<IServiceProvider, object> LiveFactory);

    private sealed class HybridServiceProxy<TService> : DispatchProxy where TService : class
    {
        private TService _primary = default!;
        private TService _fallback = default!;
        private ILogger? _logger;

        public static TService Create(TService primary, TService fallback, ILogger logger)
        {
            var proxy = Create<TService, HybridServiceProxy<TService>>();
            proxy._primary = primary;
            proxy._fallback = fallback;
            proxy._logger = logger;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            try
            {
                var result = targetMethod.Invoke(_primary, args);
                return HandleResult(targetMethod, args, result);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is HttpRequestException)
            {
                _logger?.LogWarning(ex.InnerException, "Primary API for {Service} failed, using mock fallback.", typeof(TService).Name);
                return targetMethod.Invoke(_fallback, args);
            }
        }

        private object? HandleResult(MethodInfo method, object?[]? args, object? result)
        {
            if (result is Task task)
            {
                if (method.ReturnType == typeof(Task))
                {
                    return ExecuteAsync(task, method, args);
                }

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var elementType = method.ReturnType.GenericTypeArguments[0];
                    var genericHandler = typeof(HybridServiceProxy<TService>)
                        .GetMethod(nameof(ExecuteAsyncWithResult), BindingFlags.Instance | BindingFlags.NonPublic)!
                        .MakeGenericMethod(elementType);
                    return genericHandler.Invoke(this, new object?[] { result, method, args });
                }
            }

            return result;
        }

        private async Task ExecuteAsync(Task primaryTask, MethodInfo method, object?[]? args)
        {
            try
            {
                await primaryTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (IsHttpRequestException(ex))
            {
                _logger?.LogWarning(ex, "Primary API for {Service} failed asynchronously, using mock fallback.", typeof(TService).Name);
                if (method.Invoke(_fallback, args) is Task fallbackTask)
                {
                    await fallbackTask.ConfigureAwait(false);
                }
            }
        }

        private async Task<TResult> ExecuteAsyncWithResult<TResult>(Task<TResult> primaryTask, MethodInfo method, object?[]? args)
        {
            try
            {
                return await primaryTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (IsHttpRequestException(ex))
            {
                _logger?.LogWarning(ex, "Primary API for {Service} failed asynchronously, using mock fallback.", typeof(TService).Name);
                var fallbackResult = method.Invoke(_fallback, args);
                if (fallbackResult is Task<TResult> fallbackTask)
                {
                    return await fallbackTask.ConfigureAwait(false);
                }

                return (TResult)fallbackResult!;
            }
        }

        private static bool IsHttpRequestException(Exception ex)
        {
            if (ex is HttpRequestException)
            {
                return true;
            }

            if (ex is AggregateException aggregate)
            {
                return aggregate.Flatten().InnerExceptions.Any(static inner => inner is HttpRequestException);
            }

            return false;
        }
    }
}

/// <summary>
/// Binds the per-entity data source configuration from <c>appsettings.json</c>.
/// </summary>
public sealed class DataSourceOptions
{
    public DataSourceMode Customers { get; set; } = DataSourceMode.Mock;

    public DataSourceMode Vehicles { get; set; } = DataSourceMode.Mock;

    public DataSourceMode Invoices { get; set; } = DataSourceMode.Mock;

    public DataSourceMode Appointments { get; set; } = DataSourceMode.Mock;

    public DataSourceMode Dashboard { get; set; } = DataSourceMode.Mock;
}
