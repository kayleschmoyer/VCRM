// InMemoryDashboardAnalytics.cs: Synthesizes mock KPIs, chart series, and activity feed from in-memory services.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Appointments.Models;
using CRMAdapter.UI.Services.Customers.Models;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Dashboard.Models;
using CRMAdapter.UI.Services.Invoices.Models;
using CRMAdapter.UI.Services.Vehicles.Models;

namespace CRMAdapter.UI.Services.Mock.Dashboard;

public sealed class InMemoryDashboardAnalytics : IDashboardService
{
    private readonly ICustomerService _customers;
    private readonly IVehicleService _vehicles;
    private readonly IInvoiceService _invoices;
    private readonly IAppointmentService _appointments;

    public InMemoryDashboardAnalytics(
        ICustomerService customers,
        IVehicleService vehicles,
        IInvoiceService invoices,
        IAppointmentService appointments)
    {
        _customers = customers;
        _vehicles = vehicles;
        _invoices = invoices;
        _appointments = appointments;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerSummaries = await _customers.GetCustomersAsync(cancellationToken);
        var vehicleSummaries = await _vehicles.GetVehiclesAsync(cancellationToken);
        var invoiceSummaries = await _invoices.GetInvoicesAsync(null, cancellationToken);
        var appointmentSummaries = await _appointments.GetAppointmentsAsync(null, null, null, cancellationToken);

        var totalCustomers = customerSummaries.Count;
        var activeVehicles = vehicleSummaries.Count(v => !string.Equals(v.Status, "Retired", StringComparison.OrdinalIgnoreCase));
        var outstandingInvoices = invoiceSummaries.Count(invoice => invoice.BalanceDue > 0);
        var upcomingAppointments = appointmentSummaries.Count(a => a.ScheduledStart >= DateTime.UtcNow && a.Status is not "Canceled");

        var monthlyRevenue = AggregateMonthlyRevenue(invoiceSummaries);
        var statusSlices = BuildStatusSlices(appointmentSummaries);
        var vehiclesServiced = BuildVehiclesServicedSeries(appointmentSummaries);
        var recentActivity = BuildRecentActivity(invoiceSummaries, appointmentSummaries, customerSummaries);

        return new DashboardSnapshot(
            totalCustomers,
            activeVehicles,
            outstandingInvoices,
            upcomingAppointments,
            monthlyRevenue,
            statusSlices,
            vehiclesServiced,
            recentActivity);
    }

    private static IReadOnlyList<MonthlyRevenuePoint> AggregateMonthlyRevenue(IEnumerable<InvoiceSummary> invoices)
    {
        return invoices
            .GroupBy(invoice => new { invoice.IssuedOn.Year, invoice.IssuedOn.Month })
            .OrderBy(group => new DateTime(group.Key.Year, group.Key.Month, 1))
            .Select(group => new MonthlyRevenuePoint(
                new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture),
                group.Sum(invoice => invoice.Total)))
            .ToList();
    }

    private static IReadOnlyList<AppointmentStatusSlice> BuildStatusSlices(IEnumerable<AppointmentSummary> appointments)
    {
        return appointments
            .GroupBy(appointment => appointment.Status)
            .OrderByDescending(group => group.Count())
            .Select(group => new AppointmentStatusSlice(group.Key, group.Count()))
            .ToList();
    }

    private static IReadOnlyList<VehiclesServicedPoint> BuildVehiclesServicedSeries(IEnumerable<AppointmentSummary> appointments)
    {
        return appointments
            .Where(appointment => string.Equals(appointment.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .GroupBy(appointment => new { appointment.ScheduledEnd.Year, appointment.ScheduledEnd.Month })
            .OrderBy(group => new DateTime(group.Key.Year, group.Key.Month, 1))
            .Select(group => new VehiclesServicedPoint(
                new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture),
                group.Count()))
            .ToList();
    }

    private static IReadOnlyList<RecentActivityItem> BuildRecentActivity(
        IEnumerable<InvoiceSummary> invoices,
        IEnumerable<AppointmentSummary> appointments,
        IEnumerable<CustomerSummary> customers)
    {
        var items = new List<RecentActivityItem>();

        items.AddRange(invoices
            .OrderByDescending(invoice => invoice.IssuedOn)
            .Take(5)
            .Select(invoice => new RecentActivityItem(
                invoice.IssuedOn,
                "Invoice",
                invoice.InvoiceNumber,
                $"{invoice.CustomerName} · {invoice.Status}",
                $"/invoices/{invoice.Id}")));

        items.AddRange(appointments
            .OrderByDescending(appointment => appointment.ScheduledStart)
            .Take(5)
            .Select(appointment => new RecentActivityItem(
                appointment.ScheduledStart,
                "Appointment",
                appointment.Service,
                $"{appointment.Customer.Name} · {appointment.Status}",
                $"/appointments/{appointment.Id}")));

        items.AddRange(customers
            .OrderByDescending(customer => customer.LastInvoiceDate ?? DateTime.UtcNow.AddDays(-90))
            .Take(5)
            .Select(customer => new RecentActivityItem(
                (customer.LastInvoiceDate ?? DateTime.UtcNow.AddDays(-90)).AddHours(6),
                "Customer",
                customer.Name,
                $"{customer.VehicleCount} vehicles · {customer.Email}",
                $"/customers/{customer.Id}")));

        return items
            .OrderByDescending(item => item.OccurredOn)
            .ToList();
    }
}
