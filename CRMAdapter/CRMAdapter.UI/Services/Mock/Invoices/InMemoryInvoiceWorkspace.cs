// InMemoryInvoiceWorkspace.cs: Curated invoice ledger with cross-links for prototyping financial workflows.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Invoices.Models;

namespace CRMAdapter.UI.Services.Mock.Invoices;

public sealed class InMemoryInvoiceWorkspace : IInvoiceService
{
    private readonly Dictionary<Guid, InvoiceRecord> _invoices;
    private readonly object _sync = new();

    public InMemoryInvoiceWorkspace()
    {
        _invoices = Seed().ToDictionary(invoice => invoice.Id);
    }

    public Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = search?.Trim();

        IEnumerable<InvoiceRecord> records = _invoices.Values;
        if (!string.IsNullOrWhiteSpace(query))
        {
            records = records.Where(record => Matches(record, query));
        }

        var results = records
            .OrderByDescending(record => record.IssuedOn)
            .Select(record => record.ToSummary())
            .ToList();

        return Task.FromResult<IReadOnlyList<InvoiceSummary>>(results);
    }

    public Task<InvoiceDetail?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var detail = _invoices.TryGetValue(invoiceId, out var record)
            ? record.ToDetail()
            : null;
        return Task.FromResult(detail);
    }

    public Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = _invoices.Values
            .Where(record => record.Customer.Id == customerId)
            .OrderByDescending(record => record.IssuedOn)
            .Select(record => record.ToSummary())
            .ToList();
        return Task.FromResult<IReadOnlyList<InvoiceSummary>>(results);
    }

    public Task<IReadOnlyList<InvoiceSummary>> GetInvoicesForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = _invoices.Values
            .Where(record => record.Vehicle.Id == vehicleId)
            .OrderByDescending(record => record.IssuedOn)
            .Select(record => record.ToSummary())
            .ToList();
        return Task.FromResult<IReadOnlyList<InvoiceSummary>>(results);
    }

    public Task<InvoiceDetail?> RecordPaymentAsync(Guid invoiceId, PaymentEntry payment, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_invoices.TryGetValue(invoiceId, out var record))
            {
                return Task.FromResult<InvoiceDetail?>(null);
            }

            var sanitizedAmount = Math.Max(0, Math.Round(payment.Amount, 2));
            if (sanitizedAmount <= 0)
            {
                return Task.FromResult(record.ToDetail());
            }

            var maxEligible = Math.Max(0, record.Total - record.PaymentsApplied);
            var amount = Math.Min(sanitizedAmount, maxEligible);
            if (amount <= 0 && record.BalanceDue <= 0)
            {
                return Task.FromResult(record.ToDetail());
            }

            var entry = new PaymentRecord(Guid.NewGuid(), Math.Round(amount, 2), payment.Method, payment.PaidOn, payment.Notes);
            record.AddPayment(entry);
            return Task.FromResult(record.ToDetail());
        }
    }

    private static bool Matches(InvoiceRecord record, string query)
    {
        return record.InvoiceNumber.Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.Customer.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || record.Vehicle.Vin.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<InvoiceRecord> Seed()
    {
        var invoices = new List<InvoiceRecord>
        {
            new(
                Guid.Parse("1b1f5095-94a7-41d2-93c7-74214f8da4c3"),
                "INV-3104",
                Date(2024, 12, 1),
                Date(2024, 12, 31),
                "Unpaid",
                new CustomerLink(Guid.Parse("34d7fe27-6d2d-4d4e-98d4-92f0039bbacd"), "Starlight Mobility", "support@starlightmobility.ai", "+1 (415) 555-0190"),
                new VehicleLink(Guid.Parse("17fe4cbb-08f1-4a4e-bc26-5aba25ad607c"), "WBY1Z4C58EV547221", "2024 BMW iX Shuttle"),
                new List<InvoiceLineItem>
                {
                    new("Autonomous sensor calibration", "Service", 1, 18650.00m),
                    new("Thermal management retrofit", "Service", 1, 12400.00m),
                    new("High-capacity battery module", "Part", 2, 4650.00m)
                },
                subtotal: 40350.00m,
                tax: 2017.50m,
                new List<PaymentRecord>()
            ),
            new(
                Guid.Parse("f3cd2c55-1a60-4a19-88c1-53ce84030f64"),
                "INV-3059",
                Date(2024, 9, 19),
                Date(2024, 10, 19),
                "Paid",
                new CustomerLink(Guid.Parse("34d7fe27-6d2d-4d4e-98d4-92f0039bbacd"), "Starlight Mobility", "support@starlightmobility.ai", "+1 (415) 555-0190"),
                new VehicleLink(Guid.Parse("f44f6c61-18a7-4b09-9c10-1c7c79855a34"), "JHMZE2H75AS015462", "2022 Honda Insight Fleet"),
                new List<InvoiceLineItem>
                {
                    new("Drive-by-wire diagnostics", "Service", 1, 7250.00m),
                    new("Precision lidar unit", "Part", 2, 5200.00m)
                },
                subtotal: 17650.00m,
                tax: 882.50m,
                new List<PaymentRecord>
                {
                    new(Guid.Parse("9856f3e0-9327-4882-a16c-1dc06d6da0d3"), 18532.50m, "ACH", Date(2024, 10, 2), "Auto-pay settlement")
                }
            ),
            new(
                Guid.Parse("8fb29d15-5c1a-4ad4-8664-1ffbdf1d6d53"),
                "INV-2087",
                Date(2024, 10, 29),
                Date(2024, 11, 28),
                "Unpaid",
                new CustomerLink(Guid.Parse("8b1a3dd2-f54b-4e38-bd5e-68d3e16f0ad9"), "Northwind Fleet Services", "hello@northwindfleet.com", "+1 (206) 555-0148"),
                new VehicleLink(Guid.Parse("f7b1b822-75c7-4f89-8f0f-82c4309a5ba0"), "5YJSA1E26JF275911", "2020 Tesla Model S Performance"),
                new List<InvoiceLineItem>
                {
                    new("Battery conditioning program", "Service", 1, 5250.00m),
                    new("Performance brake kit", "Part", 1, 2400.00m),
                    new("Executive detailing", "Service", 1, 850.00m)
                },
                subtotal: 8500.00m,
                tax: 425.00m,
                new List<PaymentRecord>()
            ),
            new(
                Guid.Parse("0b479f3b-7fb4-4d11-8626-8a53da7242ae"),
                "INV-2043",
                Date(2024, 8, 15),
                Date(2024, 9, 14),
                "Paid",
                new CustomerLink(Guid.Parse("8b1a3dd2-f54b-4e38-bd5e-68d3e16f0ad9"), "Northwind Fleet Services", "hello@northwindfleet.com", "+1 (206) 555-0148"),
                new VehicleLink(Guid.Parse("b058c752-2693-40ed-9be4-54d13fd88019"), "1C6SRFMT1LN245877", "2021 RAM 1500 Tradesman"),
                new List<InvoiceLineItem>
                {
                    new("Drivetrain diagnostic sweep", "Service", 1, 3420.00m),
                    new("Adaptive suspension retrofit", "Service", 1, 5600.00m)
                },
                subtotal: 9020.00m,
                tax: 451.00m,
                new List<PaymentRecord>
                {
                    new(Guid.Parse("77dca55f-35a2-475e-8bfc-d90d56f97d46"), 9471.00m, "Credit", Date(2024, 8, 28), "Corporate card - end of month")
                }
            ),
            new(
                Guid.Parse("6d842ab2-498a-480f-b26d-0c8d4dd34678"),
                "INV-1045",
                Date(2024, 11, 18),
                Date(2024, 12, 18),
                "Paid",
                new CustomerLink(Guid.Parse("d2b3f3f5-9fb5-4bcb-bf70-5c8f7a7d1a10"), "Apex Logistics", "opsdesk@apexlogistics.co", "+1 (312) 555-0188"),
                new VehicleLink(Guid.Parse("2b66a1f2-2b34-4a94-b2b3-9c7ae2d0d4fa"), "1FTEW1EP3PKA12345", "2023 Ford F-150 Lightning"),
                new List<InvoiceLineItem>
                {
                    new("Thermal battery shroud", "Part", 2, 3250.00m),
                    new("Advanced telematics install", "Service", 1, 11200.00m),
                    new("Commissioning diagnostics", "Service", 1, 4800.00m)
                },
                subtotal: 22500.00m,
                tax: 1125.00m,
                new List<PaymentRecord>
                {
                    new(Guid.Parse("b2f551e7-e748-480f-84a5-2a2f97bfb0cd"), 23625.00m, "ACH", Date(2024, 11, 25), "Autopay batch #8123")
                }
            ),
            new(
                Guid.Parse("a2b6fb7f-a9f0-45d6-99a6-1d2f3a3f5d9c"),
                "INV-1021",
                Date(2024, 9, 2),
                Date(2024, 10, 2),
                "Paid",
                new CustomerLink(Guid.Parse("d2b3f3f5-9fb5-4bcb-bf70-5c8f7a7d1a10"), "Apex Logistics", "opsdesk@apexlogistics.co", "+1 (312) 555-0188"),
                new VehicleLink(Guid.Parse("7f1ec5f0-273a-4b02-a5e3-04f5bcd00b6a"), "1GC4YVEY2NF152233", "2022 Chevrolet Silverado HD"),
                new List<InvoiceLineItem>
                {
                    new("HVAC diagnostics", "Service", 1, 2860.00m),
                    new("Fleet analytics subscription", "Service", 6, 720.00m),
                    new("High-output alternator", "Part", 1, 1825.00m)
                },
                subtotal: 9045.00m,
                tax: 452.25m,
                new List<PaymentRecord>
                {
                    new(Guid.Parse("9a9c4ab5-9c72-4cd6-8101-7a2360f91c96"), 9497.25m, "Credit", Date(2024, 9, 15), "Corporate card ending 9921")
                }
            ),
            new(
                Guid.Parse("ed22fe5a-7a63-456d-9e06-e0d0f9c2e8c9"),
                "INV-0998",
                Date(2024, 6, 27),
                Date(2024, 7, 27),
                "Overdue",
                new CustomerLink(Guid.Parse("d2b3f3f5-9fb5-4bcb-bf70-5c8f7a7d1a10"), "Apex Logistics", "opsdesk@apexlogistics.co", "+1 (312) 555-0188"),
                new VehicleLink(Guid.Parse("56d7b2d4-8b38-49be-bd23-1b4dc191f70e"), "3N1AB8CV3MY225601", "2021 Nissan Leaf Fleet"),
                new List<InvoiceLineItem>
                {
                    new("Range optimization program", "Service", 1, 9250.00m),
                    new("Battery wellness clinic", "Service", 1, 6400.00m),
                    new("Rapid-charge hardware", "Part", 1, 5120.00m)
                },
                subtotal: 20770.00m,
                tax: 1038.50m,
                new List<PaymentRecord>
                {
                    new(Guid.Parse("f28f46f3-409d-4e61-b68c-9dbf335d2f71"), 7500.00m, "ACH", Date(2024, 7, 15), "Partial remittance - AP hold")
                }
            )
        };

        foreach (var invoice in invoices)
        {
            invoice.NormalizeStatus();
        }

        return invoices;
    }

    private static DateTime Date(int year, int month, int day)
    {
        return DateTime.SpecifyKind(new DateTime(year, month, day, 0, 0, 0), DateTimeKind.Utc);
    }

    private sealed class InvoiceRecord
    {
        public InvoiceRecord(
            Guid id,
            string invoiceNumber,
            DateTime issuedOn,
            DateTime dueOn,
            string status,
            CustomerLink customer,
            VehicleLink vehicle,
            List<InvoiceLineItem> lineItems,
            decimal subtotal,
            decimal tax,
            List<PaymentRecord> payments)
        {
            Id = id;
            InvoiceNumber = invoiceNumber;
            IssuedOn = issuedOn;
            DueOn = dueOn;
            Status = status;
            Customer = customer;
            Vehicle = vehicle;
            LineItems = lineItems;
            Subtotal = Math.Round(subtotal, 2);
            Tax = Math.Round(tax, 2);
            Payments = payments;
        }

        public Guid Id { get; }
        public string InvoiceNumber { get; }
        public DateTime IssuedOn { get; }
        public DateTime DueOn { get; }
        public CustomerLink Customer { get; }
        public VehicleLink Vehicle { get; }
        public List<InvoiceLineItem> LineItems { get; }
        public decimal Subtotal { get; }
        public decimal Tax { get; }
        public List<PaymentRecord> Payments { get; }
        public string Status { get; private set; }

        public decimal Total => Math.Round(Subtotal + Tax, 2);

        public decimal PaymentsApplied => Math.Round(Payments.Sum(payment => payment.Amount), 2);

        public decimal BalanceDue => Math.Max(0, Math.Round(Total - PaymentsApplied, 2));

        public InvoiceSummary ToSummary()
        {
            return new InvoiceSummary(Id, InvoiceNumber, Customer.Id, Customer.Name, Vehicle.Id, Vehicle.Vin, IssuedOn, Status, Total, BalanceDue);
        }

        public InvoiceDetail ToDetail()
        {
            return new InvoiceDetail(Id, InvoiceNumber, IssuedOn, DueOn, Status, Customer, Vehicle, LineItems.ToList(), Subtotal, Tax, Total, PaymentsApplied, BalanceDue, Payments.OrderByDescending(p => p.PaidOn).ToList());
        }

        public void AddPayment(PaymentRecord payment)
        {
            Payments.Add(payment);
            NormalizeStatus();
        }

        public void NormalizeStatus()
        {
            if (BalanceDue <= 0)
            {
                Status = "Paid";
                return;
            }

            var today = DateTime.UtcNow.Date;
            if (DueOn.Date < today)
            {
                Status = "Overdue";
            }
            else if (!string.Equals(Status, "Overdue", StringComparison.OrdinalIgnoreCase))
            {
                Status = "Unpaid";
            }
        }
    }
}
