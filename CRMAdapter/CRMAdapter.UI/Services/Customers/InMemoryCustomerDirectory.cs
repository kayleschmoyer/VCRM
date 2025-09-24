// InMemoryCustomerDirectory.cs: Provides curated sample customers until real CRM endpoints are wired up.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Customers.Models;

namespace CRMAdapter.UI.Services.Customers;

public sealed class InMemoryCustomerDirectory : ICustomerDirectory
{
    private readonly IReadOnlyList<CustomerDetail> _customers;

    public InMemoryCustomerDirectory()
    {
        _customers = SeedCustomers();
    }

    public Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summaries = _customers
            .Select(customer => new CustomerSummary(
                customer.Id,
                customer.Name,
                customer.Phone,
                customer.Email,
                customer.Vehicles.Count,
                customer.Invoices.OrderByDescending(i => i.IssuedOn).FirstOrDefault()?.IssuedOn))
            .ToList();

        return Task.FromResult<IReadOnlyList<CustomerSummary>>(summaries);
    }

    public Task<CustomerDetail?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var customer = _customers.FirstOrDefault(c => c.Id == customerId);
        return Task.FromResult(customer);
    }

    private static IReadOnlyList<CustomerDetail> SeedCustomers()
    {
        return new List<CustomerDetail>
        {
            new(
                Guid.Parse("d2b3f3f5-9fb5-4bcb-bf70-5c8f7a7d1a10"),
                "Apex Logistics",
                "opsdesk@apexlogistics.co",
                "+1 (312) 555-0188",
                "National 3PL partner prioritizing electrified fleets and predictive maintenance programs.",
                new List<VehicleRecord>
                {
                    new(Guid.Parse("2b66a1f2-2b34-4a94-b2b3-9c7ae2d0d4fa"), "1FTEW1EP3PKA12345", "2023", "Ford F-150 Lightning", "Active"),
                    new(Guid.Parse("7f1ec5f0-273a-4b02-a5e3-04f5bcd00b6a"), "1GC4YVEY2NF152233", "2022", "Chevrolet Silverado HD", "In service"),
                    new(Guid.Parse("56d7b2d4-8b38-49be-bd23-1b4dc191f70e"), "3N1AB8CV3MY225601", "2021", "Nissan Leaf Fleet", "Active")
                },
                new List<InvoiceRecord>
                {
                    new("INV-1045", CreateDate(2024, 11, 18), 24800.00m, "Paid"),
                    new("INV-1021", CreateDate(2024, 9, 2), 18740.50m, "Paid"),
                    new("INV-0998", CreateDate(2024, 6, 27), 22110.75m, "Past due")
                },
                new List<AppointmentRecord>
                {
                    new(CreateDate(2024, 12, 2, 14, 0), "Q1 capacity planning", "Mia Chen", "Scheduled"),
                    new(CreateDate(2024, 11, 8, 10, 30), "Fleet telemetry rollout", "Derrick James", "Completed")
                }
            ),
            new(
                Guid.Parse("8b1a3dd2-f54b-4e38-bd5e-68d3e16f0ad9"),
                "Northwind Fleet Services",
                "hello@northwindfleet.com",
                "+1 (206) 555-0148",
                "Regional service network delivering rapid-response maintenance for commercial EVs.",
                new List<VehicleRecord>
                {
                    new(Guid.Parse("f7b1b822-75c7-4f89-8f0f-82c4309a5ba0"), "5YJSA1E26JF275911", "2020", "Tesla Model S Performance", "Active"),
                    new(Guid.Parse("b058c752-2693-40ed-9be4-54d13fd88019"), "1C6SRFMT1LN245877", "2021", "RAM 1500 Tradesman", "Awaiting parts")
                },
                new List<InvoiceRecord>
                {
                    new("INV-2087", CreateDate(2024, 10, 29), 9450.00m, "Processing"),
                    new("INV-2043", CreateDate(2024, 8, 15), 11220.00m, "Paid"),
                    new("INV-2001", CreateDate(2024, 5, 3), 6580.00m, "Paid")
                },
                new List<AppointmentRecord>
                {
                    new(CreateDate(2024, 11, 21, 9, 0), "Battery wellness audit", "Priya Patel", "Scheduled"),
                    new(CreateDate(2024, 10, 5, 16, 0), "Emergency roadside review", "Leo Zhang", "Completed"),
                    new(CreateDate(2024, 9, 12, 11, 30), "Executive business review", "Avery Ross", "Completed")
                }
            ),
            new(
                Guid.Parse("34d7fe27-6d2d-4d4e-98d4-92f0039bbacd"),
                "Starlight Mobility",
                "support@starlightmobility.ai",
                "+1 (415) 555-0190",
                "Autonomous shuttle innovator leveraging CRM Adapter to orchestrate deployments.",
                new List<VehicleRecord>
                {
                    new(Guid.Parse("17fe4cbb-08f1-4a4e-bc26-5aba25ad607c"), "WBY1Z4C58EV547221", "2024", "BMW iX Shuttle", "Active"),
                    new(Guid.Parse("f44f6c61-18a7-4b09-9c10-1c7c79855a34"), "JHMZE2H75AS015462", "2022", "Honda Insight Fleet", "In diagnostics"),
                    new(Guid.Parse("2b331d6c-7fb2-4a42-955a-01f8a0fa7a62"), "2C4RC1BG5HR677451", "2021", "Chrysler Pacifica Hybrid", "Active"),
                    new(Guid.Parse("73a1f2fe-6475-4b47-8178-3bfe5d9dd26a"), "1HGCV1F39LA067811", "2020", "Honda Accord Hybrid", "Retired")
                },
                new List<InvoiceRecord>
                {
                    new("INV-3104", CreateDate(2024, 12, 1), 40250.00m, "Draft"),
                    new("INV-3059", CreateDate(2024, 9, 19), 38990.00m, "Paid"),
                    new("INV-2995", CreateDate(2024, 7, 23), 41575.00m, "Paid")
                },
                new List<AppointmentRecord>
                {
                    new(CreateDate(2024, 12, 15, 13, 0), "Pilot launch readiness", "Jordan Blake", "Scheduled"),
                    new(CreateDate(2024, 11, 2, 15, 30), "ADAS analytics review", "Nora Alvarez", "Scheduled"),
                    new(CreateDate(2024, 8, 28, 10, 0), "Safety certification retro", "Isabella Moore", "Completed")
                }
            )
        };
    }

    private static DateTime CreateDate(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, 0), DateTimeKind.Utc);
    }
}
