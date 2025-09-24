// InMemoryAppointmentBook.cs: Provides curated appointment data with cross-entity lookups for the prototype UI.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Appointments.Models;

namespace CRMAdapter.UI.Services.Appointments;

public sealed class InMemoryAppointmentBook : IAppointmentBook
{
    private readonly Dictionary<Guid, AppointmentSeedRecord> _records;

    public InMemoryAppointmentBook()
    {
        _records = AppointmentSeedData.Records.ToDictionary(record => record.Id);
    }

    public Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsAsync(
        DateTime? start = null,
        DateTime? end = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<AppointmentSeedRecord> query = _records.Values;

        if (start.HasValue)
        {
            query = query.Where(record => record.ScheduledStart >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(record => record.ScheduledStart <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(record => string.Equals(record.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        var summaries = query
            .OrderBy(record => record.ScheduledStart)
            .Select(CreateSummary)
            .ToList();

        return Task.FromResult<IReadOnlyList<AppointmentSummary>>(summaries);
    }

    public Task<AppointmentDetail?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var detail = _records.TryGetValue(appointmentId, out var record)
            ? CreateDetail(record)
            : null;
        return Task.FromResult(detail);
    }

    public Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summaries = _records.Values
            .Where(record => record.CustomerId == customerId)
            .OrderBy(record => record.ScheduledStart)
            .Select(CreateSummary)
            .ToList();
        return Task.FromResult<IReadOnlyList<AppointmentSummary>>(summaries);
    }

    public Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summaries = _records.Values
            .Where(record => record.VehicleId == vehicleId)
            .OrderBy(record => record.ScheduledStart)
            .Select(CreateSummary)
            .ToList();
        return Task.FromResult<IReadOnlyList<AppointmentSummary>>(summaries);
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetUpcomingAppointmentsAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTime.UtcNow;
        var results = (await GetAppointmentsAsync(now, null, null, cancellationToken))
            .Take(count)
            .ToList();
        return results;
    }

    public Task<IReadOnlyList<string>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var statuses = _records.Values
            .Select(record => record.Status)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(status => status)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(statuses);
    }

    internal static AppointmentSummary CreateSummary(AppointmentSeedRecord record)
    {
        return new AppointmentSummary(
            record.Id,
            record.AppointmentNumber,
            record.ScheduledStart,
            record.ScheduledStart.Add(record.Duration),
            record.Status,
            record.Service,
            record.Technician,
            new AppointmentLinkedCustomer(record.CustomerId, record.CustomerName, record.CustomerEmail, record.CustomerPhone),
            new AppointmentLinkedVehicle(record.VehicleId, record.VehicleVin, record.VehicleDisplay, record.VehicleStatus),
            string.IsNullOrWhiteSpace(record.Notes)
                ? null
                : record.Notes.Length > 120
                    ? record.Notes[..120] + "…"
                    : record.Notes);
    }

    internal static AppointmentDetail CreateDetail(AppointmentSeedRecord record)
    {
        return new AppointmentDetail(
            record.Id,
            record.AppointmentNumber,
            record.ScheduledStart,
            record.ScheduledStart.Add(record.Duration),
            record.Status,
            record.Service,
            record.Description,
            record.Technician,
            record.Location,
            record.Notes,
            new AppointmentLinkedCustomer(record.CustomerId, record.CustomerName, record.CustomerEmail, record.CustomerPhone),
            new AppointmentLinkedVehicle(record.VehicleId, record.VehicleVin, record.VehicleDisplay, record.VehicleStatus),
            record.CreatedOn,
            record.LastUpdatedOn);
    }
}
