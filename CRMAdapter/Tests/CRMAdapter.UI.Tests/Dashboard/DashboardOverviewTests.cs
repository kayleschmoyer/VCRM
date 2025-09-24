// DashboardOverviewTests.cs: Ensures dashboard KPIs, charts, and activity feed render from mock services.
using Bunit;
using CRMAdapter.UI.Pages.Dashboard;
using CRMAdapter.UI.Services.Appointments;
using CRMAdapter.UI.Services.Customers;
using CRMAdapter.UI.Services.Dashboard;
using CRMAdapter.UI.Services.Invoices;
using CRMAdapter.UI.Services.Vehicles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace CRMAdapter.UI.Tests.Dashboard;

public sealed class DashboardOverviewTests : TestContext
{
    public DashboardOverviewTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<ICustomerDirectory, InMemoryCustomerDirectory>();
        Services.AddSingleton<IVehicleRegistry, InMemoryVehicleRegistry>();
        Services.AddSingleton<IInvoiceWorkspace, InMemoryInvoiceWorkspace>();
        Services.AddSingleton<IAppointmentBook, InMemoryAppointmentBook>();
        Services.AddSingleton<IDashboardAnalytics, InMemoryDashboardAnalytics>();
    }

    [Fact]
    public void Dashboard_ShouldRenderKpis()
    {
        var component = RenderComponent<Overview>();

        component.WaitForAssertion(() =>
        {
            component.FindAll(".crm-kpi-value").Should().HaveCount(4);
        });
    }

    [Fact]
    public void Dashboard_ShouldRenderChartsAndActivity()
    {
        var component = RenderComponent<Overview>();

        component.WaitForAssertion(() => component.FindAll("canvas").Should().NotBeEmpty());
        component.WaitForAssertion(() => component.Find("table").Should().NotBeNull());
    }
}
