// AppointmentsModuleTests.cs: Covers appointments list, detail, and schedule interactions.
using System;
using System.Linq;
using Bunit;
using Bunit.TestDoubles;
using CRMAdapter.UI.Pages.Appointments;
using CRMAdapter.UI.Services.Appointments;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace CRMAdapter.UI.Tests.Appointments;

public sealed class AppointmentsModuleTests : TestContext
{
    public AppointmentsModuleTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<IAppointmentBook, InMemoryAppointmentBook>();
    }

    [Fact]
    public void List_ShouldRenderAppointmentsAndFilterByStatus()
    {
        var component = RenderComponent<List>();

        component.WaitForAssertion(() => component.FindAll("table tbody tr").Should().NotBeEmpty());

        var statusSelect = component.Find("div.mud-select");
        statusSelect.Click();

        component.WaitForAssertion(() => component.FindAll("div.mud-list-item").Should().NotBeEmpty());
        var completedOption = component.FindAll("div.mud-list-item").First(item => item.TextContent.Contains("Completed"));
        completedOption.Click();

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().NotBeEmpty();
            rows.All(row => row.TextContent.Contains("Completed")).Should().BeTrue();
        });
    }

    [Fact]
    public void Detail_ShouldExposeCustomerAndVehicleLinks()
    {
        var book = Services.GetRequiredService<IAppointmentBook>();
        var appointment = book.GetAppointmentsAsync().GetAwaiter().GetResult().First();

        var component = RenderComponent<Detail>(parameters => parameters.Add(p => p.AppointmentId, appointment.Id));

        component.WaitForAssertion(() =>
        {
            var customerLink = component.Find("a[data-cy='customer-link']");
            customerLink.GetAttribute("href").Should().Contain(appointment.Customer.Id.ToString());

            var vehicleLink = component.Find("a[data-cy='vehicle-link']");
            vehicleLink.GetAttribute("href").Should().Contain(appointment.Vehicle.Id.ToString());
        });
    }

    [Fact]
    public void Schedule_CardClickNavigatesToDetail()
    {
        var nav = Services.GetRequiredService<NavigationManager>() as TestNavigationManager;
        var component = RenderComponent<Schedule>();

        component.WaitForAssertion(() => component.FindAll(".crm-appointment-card").Should().NotBeEmpty());

        var firstCard = component.Find(".crm-appointment-card");
        firstCard.Click();

        nav!.Uri.Should().Contain("/appointments/");
    }
}
