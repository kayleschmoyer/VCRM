// HybridDataSourceTests.cs: Validates the hybrid resolver chooses mock or live services based on configuration.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.DataSource;
using CRMAdapter.UI.Services.Api.Appointments;
using CRMAdapter.UI.Services.Api.Customers;
using CRMAdapter.UI.Services.Api.Dashboard;
using CRMAdapter.UI.Services.Api.Invoices;
using CRMAdapter.UI.Services.Api.Vehicles;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Mock.Appointments;
using CRMAdapter.UI.Services.Mock.Customers;
using CRMAdapter.UI.Services.Mock.Dashboard;
using CRMAdapter.UI.Services.Mock.Invoices;
using CRMAdapter.UI.Services.Mock.Vehicles;
using CRMAdapter.UI.Services.Customers.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CRMAdapter.UI.Tests.Hybrid;

public class HybridDataSourceTests
{
    [Fact]
    public void ReturnsMockImplementationsWhenConfiguredForMock()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["DataSource:Customers"] = "Mock",
            ["DataSource:Vehicles"] = "Mock",
            ["DataSource:Invoices"] = "Mock",
            ["DataSource:Appointments"] = "Mock",
            ["DataSource:Dashboard"] = "Mock"
        });

        using var scope = provider.CreateScope();
        var strategy = scope.ServiceProvider.GetRequiredService<IDataSourceStrategy>();

        strategy.GetService<ICustomerService>().Should().BeOfType<InMemoryCustomerDirectory>();
        strategy.GetService<IVehicleService>().Should().BeOfType<InMemoryVehicleRegistry>();
        strategy.GetService<IInvoiceService>().Should().BeOfType<InMemoryInvoiceWorkspace>();
        strategy.GetService<IAppointmentService>().Should().BeOfType<InMemoryAppointmentBook>();
        strategy.GetService<IDashboardService>().Should().BeOfType<InMemoryDashboardAnalytics>();
    }

    [Fact]
    public void ReturnsApiImplementationsWhenConfiguredForLive()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["DataSource:Customers"] = "Live",
            ["DataSource:Vehicles"] = "Live",
            ["DataSource:Invoices"] = "Live",
            ["DataSource:Appointments"] = "Live",
            ["DataSource:Dashboard"] = "Live"
        });

        using var scope = provider.CreateScope();
        var strategy = scope.ServiceProvider.GetRequiredService<IDataSourceStrategy>();

        strategy.GetService<ICustomerService>().Should().BeOfType<CustomerApiClient>();
        strategy.GetService<IVehicleService>().Should().BeOfType<VehicleApiClient>();
        strategy.GetService<IInvoiceService>().Should().BeOfType<InvoiceApiClient>();
        strategy.GetService<IAppointmentService>().Should().BeOfType<AppointmentApiClient>();
        strategy.GetService<IDashboardService>().Should().BeOfType<DashboardApiClient>();
    }

    [Fact]
    public async Task AutoModeFallsBackToMockWhenLiveThrowsHttpRequestException()
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["DataSource:Customers"] = "Auto",
                ["DataSource:Vehicles"] = "Mock",
                ["DataSource:Invoices"] = "Mock",
                ["DataSource:Appointments"] = "Mock",
                ["DataSource:Dashboard"] = "Mock"
            },
            services =>
            {
                services.AddScoped<ThrowingCustomerApiClient>();
                services.AddScoped<CustomerApiClient>(sp => sp.GetRequiredService<ThrowingCustomerApiClient>());
            });

        using var scope = provider.CreateScope();
        var strategy = scope.ServiceProvider.GetRequiredService<IDataSourceStrategy>();
        var service = strategy.GetService<ICustomerService>();
        var mock = scope.ServiceProvider.GetRequiredService<InMemoryCustomerDirectory>();
        var throwingClient = scope.ServiceProvider.GetRequiredService<ThrowingCustomerApiClient>();

        var expected = await mock.GetCustomersAsync();
        var actual = await service.GetCustomersAsync();

        actual.Should().BeEquivalentTo(expected);
        throwingClient.InvocationCount.Should().Be(1);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddOptions();
        services.Configure<DataSourceOptions>(configuration.GetSection("DataSource"));

        services.AddScoped<InMemoryCustomerDirectory>();
        services.AddScoped<InMemoryVehicleRegistry>();
        services.AddScoped<InMemoryInvoiceWorkspace>();
        services.AddScoped<InMemoryAppointmentBook>();
        services.AddScoped<InMemoryDashboardAnalytics>();

        void ConfigureClient(HttpClient client) => client.BaseAddress = new Uri("https://localhost");

        services.AddHttpClient<CustomerApiClient>(ConfigureClient);
        services.AddHttpClient<VehicleApiClient>(ConfigureClient);
        services.AddHttpClient<InvoiceApiClient>(ConfigureClient);
        services.AddHttpClient<AppointmentApiClient>(ConfigureClient);
        services.AddHttpClient<DashboardApiClient>(ConfigureClient);

        services.AddScoped<IDataSourceStrategy, DataSourceStrategy>();
        services.AddScoped<ICustomerService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<ICustomerService>());
        services.AddScoped<IVehicleService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IVehicleService>());
        services.AddScoped<IInvoiceService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IInvoiceService>());
        services.AddScoped<IAppointmentService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IAppointmentService>());
        services.AddScoped<IDashboardService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IDashboardService>());

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private sealed class ThrowingCustomerApiClient : CustomerApiClient
    {
        public ThrowingCustomerApiClient(InMemoryCustomerDirectory mock)
            : base(new HttpClient { BaseAddress = new Uri("https://localhost") }, mock)
        {
        }

        public int InvocationCount { get; private set; }

        public new Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new HttpRequestException("Simulated API failure");
        }

        public new Task<CustomerDetail?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new HttpRequestException("Simulated API failure");
        }
    }
}
