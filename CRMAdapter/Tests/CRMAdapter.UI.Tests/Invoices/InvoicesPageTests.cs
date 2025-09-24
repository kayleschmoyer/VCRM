// InvoicesPageTests.cs: Integration coverage for invoice listing, detail rendering, and payment simulation flows.
using System.Globalization;
using System.Linq;
using Bunit;
using CRMAdapter.UI.Pages.Invoices;
using CRMAdapter.UI.Services.Invoices;
using CRMAdapter.UI.Services.Invoices.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace CRMAdapter.UI.Tests.Invoices;

public sealed class InvoicesPageTests : TestContext
{
    public InvoicesPageTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<IInvoiceWorkspace, InMemoryInvoiceWorkspace>();
    }

    [Fact]
    public void InvoicesList_ShouldRenderMockInvoices()
    {
        var component = RenderComponent<List>();

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().NotBeEmpty();
        });
    }

    [Fact]
    public void InvoicesList_SearchFiltersByInvoiceNumber()
    {
        var component = RenderComponent<List>();

        component.WaitForAssertion(() => component.FindAll("table tbody tr").Count.Should().BeGreaterThan(0));

        var input = component.Find("input");
        input.Change("INV-0998");

        component.WaitForAssertion(() =>
        {
            var rows = component.FindAll("table tbody tr");
            rows.Should().HaveCount(1);
            rows[0].TextContent.Should().Contain("INV-0998");
        });
    }

    [Fact]
    public void InvoiceDetail_ShouldRenderHeaderSummaryAndLines()
    {
        var workspace = Services.GetRequiredService<IInvoiceWorkspace>();
        var invoice = workspace.GetInvoicesAsync().GetAwaiter().GetResult().First();

        var detail = RenderComponent<Detail>(parameters => parameters.Add(p => p.InvoiceId, invoice.Id));

        detail.WaitForAssertion(() =>
        {
            detail.Markup.Should().Contain("Summary");
            detail.Markup.Should().Contain("Line items");
            detail.Markup.Should().Contain(invoice.InvoiceNumber);
        });
    }

    [Fact]
    public void InvoiceDetail_RecordPaymentUpdatesBalance()
    {
        var workspace = Services.GetRequiredService<IInvoiceWorkspace>();
        var outstanding = workspace.GetInvoicesAsync().GetAwaiter().GetResult().First(i => i.BalanceDue > 0);

        var detail = RenderComponent<Detail>(parameters => parameters.Add(p => p.InvoiceId, outstanding.Id));

        detail.WaitForAssertion(() => detail.Find("button[data-testid='record-payment-button']"));

        detail.Find("button[data-testid='record-payment-button']").Click();

        detail.WaitForAssertion(() => detail.Find("input[data-testid='payment-amount']"));

        detail.Find("button[data-testid='record-payment-submit']").Click();

        var expected = string.Format(CultureInfo.CurrentCulture, "{0:C}", 0m);

        detail.WaitForAssertion(() =>
        {
            var balanceElement = detail.Find(".crm-balance-clear");
            balanceElement.TextContent.Should().Contain(expected);
        });

        detail.WaitForAssertion(() => detail.Markup.Should().Contain("Paid"));
    }
}
