// CustomersPageTests.cs: Integration coverage for the customer listing and detail experiences.
using System.Linq;
using Bunit;
using CRMAdapter.UI.Pages.Customers;
using CRMAdapter.UI.Services.Customers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace CRMAdapter.UI.Tests.Customers;

public sealed class CustomersPageTests : TestContext
{
    public CustomersPageTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<ICustomerDirectory, InMemoryCustomerDirectory>();
    }

    [Fact]
    public void CustomersList_ShouldRenderMockCustomers()
    {
        var component = RenderComponent<List>();

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().NotBeEmpty();
        });
    }

    [Fact]
    public void CustomersList_SearchFiltersRows()
    {
        var component = RenderComponent<List>();

        component.WaitForAssertion(() => component.FindAll("table tbody tr").Count.Should().BeGreaterThan(0));

        var input = component.Find("input");
        input.Change("Starlight");

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().HaveCount(1);
            rows[0].TextContent.Should().Contain("Starlight Mobility");
        });
    }

    [Fact]
    public void CustomerDetail_ShouldRenderAllTabs()
    {
        var directory = Services.GetRequiredService<ICustomerDirectory>();
        var firstCustomer = directory.GetCustomersAsync().GetAwaiter().GetResult().First();

        var detail = RenderComponent<Detail>(parameters => parameters.Add(p => p.CustomerId, firstCustomer.Id));

        detail.WaitForAssertion(() =>
        {
            var tabs = detail.FindAll("button[role='tab']");
            tabs.Select(t => t.TextContent.Trim()).Should().Contain(new[] { "Vehicles", "Invoices", "Appointments" });
        });
    }
}
