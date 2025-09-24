// InMemoryVehicleRegistry.cs: Supplies curated vehicles with cross-linked customers for prototyping experiences.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Mock.Appointments;
using CRMAdapter.UI.Services.Vehicles.Models;

namespace CRMAdapter.UI.Services.Mock.Vehicles;

public sealed class InMemoryVehicleRegistry : IVehicleService
{
    private readonly IReadOnlyList<VehicleDetail> _vehicles;

    public InMemoryVehicleRegistry()
    {
        _vehicles = SeedVehicles();
    }

    public Task<IReadOnlyList<VehicleSummary>> GetVehiclesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summaries = _vehicles
            .Select(vehicle => new VehicleSummary(
                vehicle.Id,
                vehicle.Vin,
                vehicle.Year,
                vehicle.Make,
                vehicle.Model,
                vehicle.Owner.Id,
                vehicle.Owner.Name,
                vehicle.Plate,
                vehicle.Status,
                vehicle.LastServiceDate))
            .OrderByDescending(v => v.LastServiceDate ?? DateTime.MinValue)
            .ThenBy(v => v.CustomerName)
            .ToList();

        return Task.FromResult<IReadOnlyList<VehicleSummary>>(summaries);
    }

    public Task<VehicleDetail?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vehicle = _vehicles.FirstOrDefault(v => v.Id == vehicleId);
        return Task.FromResult(vehicle);
    }

    public Task<VehicleDetail> SaveVehicleAsync(VehicleDetail vehicle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(vehicle);
    }

    private static IReadOnlyList<VehicleDetail> SeedVehicles()
    {
        var appointmentsByVehicle = AppointmentSeedData.Records
            .GroupBy(record => record.VehicleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(r => r.ScheduledStart)
                    .Select(r => new VehicleAppointmentRecord(r.Id, r.ScheduledStart, r.Service, r.Technician, r.Status))
                    .ToList());

        return new List<VehicleDetail>
        {
            new(
                Guid.Parse("2b66a1f2-2b34-4a94-b2b3-9c7ae2d0d4fa"),
                "1FTEW1EP3PKA12345",
                2023,
                "Ford",
                "F-150 Lightning",
                "APL-4821",
                "Active",
                "Primary electric truck covering smart city distribution routes.",
                new VehicleOwner(Guid.Parse("d2b3f3f5-9fb5-4bcb-bf70-5c8f7a7d1a10"), "Apex Logistics", "Mia Chen"),
                CreateDate(2024, 11, 18),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-1045", CreateDate(2024, 11, 18), 24800.00m, "Paid"),
                    new("INV-1010", CreateDate(2024, 8, 22), 17250.00m, "Paid"),
                    new("INV-0975", CreateDate(2024, 5, 30), 18940.00m, "Paid")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("2b66a1f2-2b34-4a94-b2b3-9c7ae2d0d4fa"), out var f150Appointments)
                    ? f150Appointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("7f1ec5f0-273a-4b02-a5e3-04f5bcd00b6a"),
                "1GC4YVEY2NF152233",
                2022,
                "Chevrolet",
                "Silverado HD",
                "APL-7719",
                "In service",
                "Heavy-duty platform undergoing telematics retrofit program.",
                new VehicleOwner(Guid.Parse("d2b3f3f5-9fb5-4bcb-bf70-5c8f7a7d1a10"), "Apex Logistics", "Derrick James"),
                CreateDate(2024, 10, 29),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-1021", CreateDate(2024, 9, 2), 18740.50m, "Paid"),
                    new("INV-0988", CreateDate(2024, 6, 14), 16330.00m, "Processing")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("7f1ec5f0-273a-4b02-a5e3-04f5bcd00b6a"), out var silveradoAppointments)
                    ? silveradoAppointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("56d7b2d4-8b38-49be-bd23-1b4dc191f70e"),
                "3N1AB8CV3MY225601",
                2021,
                "Nissan",
                "Leaf Fleet",
                "APL-2395",
                "Active",
                "Urban delivery pod optimized for multi-drop logistics.",
                new VehicleOwner(Guid.Parse("d2b3f3f5-9fb5-4bcb-bf70-5c8f7a7d1a10"), "Apex Logistics", "Mia Chen"),
                CreateDate(2024, 9, 2),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-0998", CreateDate(2024, 6, 27), 22110.75m, "Past due"),
                    new("INV-0951", CreateDate(2024, 3, 5), 15480.25m, "Paid")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("56d7b2d4-8b38-49be-bd23-1b4dc191f70e"), out var leafAppointments)
                    ? leafAppointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("f7b1b822-75c7-4f89-8f0f-82c4309a5ba0"),
                "5YJSA1E26JF275911",
                2020,
                "Tesla",
                "Model S Performance",
                "NFL-8852",
                "Active",
                "Executive transport asset with concierge maintenance plan.",
                new VehicleOwner(Guid.Parse("8b1a3dd2-f54b-4e38-bd5e-68d3e16f0ad9"), "Northwind Fleet Services", "Priya Patel"),
                CreateDate(2024, 10, 29),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-2087", CreateDate(2024, 10, 29), 9450.00m, "Processing"),
                    new("INV-2043", CreateDate(2024, 8, 15), 11220.00m, "Paid")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("f7b1b822-75c7-4f89-8f0f-82c4309a5ba0"), out var modelSAppointments)
                    ? modelSAppointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("b058c752-2693-40ed-9be4-54d13fd88019"),
                "1C6SRFMT1LN245877",
                2021,
                "RAM",
                "1500 Tradesman",
                "NFL-6720",
                "Awaiting parts",
                "Light-duty support vehicle paused pending drivetrain replacement.",
                new VehicleOwner(Guid.Parse("8b1a3dd2-f54b-4e38-bd5e-68d3e16f0ad9"), "Northwind Fleet Services", "Leo Zhang"),
                CreateDate(2024, 8, 15),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-2001", CreateDate(2024, 5, 3), 6580.00m, "Paid"),
                    new("INV-1960", CreateDate(2024, 2, 12), 4875.00m, "Draft")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("b058c752-2693-40ed-9be4-54d13fd88019"), out var ramAppointments)
                    ? ramAppointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("17fe4cbb-08f1-4a4e-bc26-5aba25ad607c"),
                "WBY1Z4C58EV547221",
                2024,
                "BMW",
                "iX Shuttle",
                "STM-4410",
                "Active",
                "Autonomous-ready shuttle configured for hospitality routes.",
                new VehicleOwner(Guid.Parse("34d7fe27-6d2d-4d4e-98d4-92f0039bbacd"), "Starlight Mobility", "Jordan Blake"),
                CreateDate(2024, 12, 1),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-3104", CreateDate(2024, 12, 1), 40250.00m, "Draft"),
                    new("INV-3059", CreateDate(2024, 9, 19), 38990.00m, "Paid")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("17fe4cbb-08f1-4a4e-bc26-5aba25ad607c"), out var ixAppointments)
                    ? ixAppointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("f44f6c61-18a7-4b09-9c10-1c7c79855a34"),
                "JHMZE2H75AS015462",
                2022,
                "Honda",
                "Insight Fleet",
                "STM-8824",
                "In diagnostics",
                "Autonomy sensor suite undergoing recalibration after firmware update.",
                new VehicleOwner(Guid.Parse("34d7fe27-6d2d-4d4e-98d4-92f0039bbacd"), "Starlight Mobility", "Nora Alvarez"),
                CreateDate(2024, 11, 2),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-2995", CreateDate(2024, 7, 23), 41575.00m, "Paid"),
                    new("INV-2940", CreateDate(2024, 4, 11), 36840.00m, "Paid")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("f44f6c61-18a7-4b09-9c10-1c7c79855a34"), out var insightAppointments)
                    ? insightAppointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("2b331d6c-7fb2-4a42-955a-01f8a0fa7a62"),
                "2C4RC1BG5HR677451",
                2021,
                "Chrysler",
                "Pacifica Hybrid",
                "STM-5521",
                "Active",
                "Mobility service vehicle configured for accessible transport routes.",
                new VehicleOwner(Guid.Parse("34d7fe27-6d2d-4d4e-98d4-92f0039bbacd"), "Starlight Mobility", "Isabella Moore"),
                CreateDate(2024, 9, 19),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-3059", CreateDate(2024, 9, 19), 38990.00m, "Paid"),
                    new("INV-2882", CreateDate(2024, 3, 28), 27410.00m, "Paid")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("2b331d6c-7fb2-4a42-955a-01f8a0fa7a62"), out var pacificaAppointments)
                    ? pacificaAppointments
                    : new List<VehicleAppointmentRecord>()
            ),
            new(
                Guid.Parse("73a1f2fe-6475-4b47-8178-3bfe5d9dd26a"),
                "1HGCV1F39LA067811",
                2020,
                "Honda",
                "Accord Hybrid",
                "STM-1190",
                "Retired",
                "Legacy asset retained for analytics benchmarking and parts harvesting.",
                new VehicleOwner(Guid.Parse("34d7fe27-6d2d-4d4e-98d4-92f0039bbacd"), "Starlight Mobility", "Nora Alvarez"),
                CreateDate(2024, 6, 1),
                new List<VehicleInvoiceRecord>
                {
                    new("INV-2766", CreateDate(2024, 1, 17), 19800.00m, "Paid")
                },
                appointmentsByVehicle.TryGetValue(Guid.Parse("73a1f2fe-6475-4b47-8178-3bfe5d9dd26a"), out var accordAppointments)
                    ? accordAppointments
                    : new List<VehicleAppointmentRecord>()
            )
        };
    }

    private static DateTime CreateDate(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return DateTime.SpecifyKind(new DateTime(year, month, day, hour, minute, 0), DateTimeKind.Utc);
    }
}
