// VehiclesPageTests.cs: Integration coverage for the vehicle listing and detail experiences.
using System.Linq;
using Bunit;
using CRMAdapter.UI.Pages.Vehicles;
using CRMAdapter.UI.Services.Vehicles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace CRMAdapter.UI.Tests.Vehicles;

public sealed class VehiclesPageTests : TestContext
{
    public VehiclesPageTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<IVehicleRegistry, InMemoryVehicleRegistry>();
    }

    [Fact]
    public void VehiclesList_ShouldRenderMockVehicles()
    {
        var component = RenderComponent<List>();

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().NotBeEmpty();
        });
    }

    [Fact]
    public void VehiclesList_SearchFiltersByVinOrOwner()
    {
        var component = RenderComponent<List>();

        component.WaitForAssertion(() => component.FindAll("table tbody tr").Count.Should().BeGreaterThan(0));

        var input = component.Find("input");
        input.Change("Apex");

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().HaveCount(3);
            rows[0].TextContent.Should().Contain("Apex Logistics");
        });

        input.Change("1FTEW1EP3PKA12345");

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().HaveCount(1);
            rows[0].TextContent.Should().Contain("1FTEW1EP3PKA12345");
        });
    }

    [Fact]
    public void VehicleDetail_ShouldRenderTabsAndOwnerLink()
    {
        var registry = Services.GetRequiredService<IVehicleRegistry>();
        var firstVehicle = registry.GetVehiclesAsync().GetAwaiter().GetResult().First();

        var detail = RenderComponent<Detail>(parameters => parameters.Add(p => p.VehicleId, firstVehicle.Id));

        detail.WaitForAssertion(() =>
        {
            var tabs = detail.FindAll("button[role='tab']");
            tabs.Select(t => t.TextContent.Trim()).Should().Contain(new[] { "Invoices", "Appointments" });

            var ownerLink = detail.FindAll("a").FirstOrDefault(a => a.GetAttribute("href")?.Contains("/customers/") == true);
            ownerLink.Should().NotBeNull();
            ownerLink!.GetAttribute("href").Should().Contain(firstVehicle.CustomerId.ToString());
        });
    }
}
