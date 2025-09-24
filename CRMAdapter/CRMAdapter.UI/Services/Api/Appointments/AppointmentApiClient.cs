// AppointmentApiClient.cs: HTTP client facade for appointment operations, currently serving mock data.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Core.Storage;
using CRMAdapter.UI.Core.Sync;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Appointments.Models;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Mock.Appointments;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Services.Api.Appointments;

public sealed class AppointmentApiClient : BaseApiClient, IAppointmentService
{
    private readonly InMemoryAppointmentBook _mock;
    private readonly ILocalCache _cache;
    private readonly ISyncQueue _syncQueue;
    private readonly OfflineSyncState _syncState;
    private readonly ILogger<AppointmentApiClient> _logger;

    public AppointmentApiClient(
        HttpClient client,
        InMemoryAppointmentBook mock,
        ILocalCache cache,
        ISyncQueue syncQueue,
        OfflineSyncState syncState,
        ILogger<AppointmentApiClient> logger)
        : base(client)
    {
        _mock = mock;
        _cache = cache;
        _syncQueue = syncQueue;
        _syncState = syncState;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsAsync(
        DateTime? start = null,
        DateTime? end = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = BuildRangeQuery(start, end, status);
            var response = await Client.GetAsync($"appointments{query}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<AppointmentSummary>();
            foreach (var appointment in appointments)
            {
                await _cache.SetAsync(appointment.Id.ToString(), appointment, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return appointments;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached appointments due to API failure.");
            _syncState.SetOffline(true);
            var cached = await _cache.GetAllAsync<AppointmentSummary>(cancellationToken).ConfigureAwait(false);
            return FilterAppointments(cached, start, end, status);
        }
    }

    public async Task<AppointmentDetail?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"appointments/{appointmentId}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var detail = await response.Content.ReadFromJsonAsync<AppointmentDetail>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (detail is not null)
            {
                await CacheAppointmentAsync(detail, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return detail;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falling back to cached appointment {AppointmentId} due to API failure.", appointmentId);
            _syncState.SetOffline(true);
            var cached = await _cache.GetAsync<AppointmentDetail>(appointmentId.ToString(), cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            return await _mock.GetAppointmentAsync(appointmentId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"customers/{customerId}/appointments", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<AppointmentSummary>();
            foreach (var appointment in appointments)
            {
                await _cache.SetAsync(appointment.Id.ToString(), appointment, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return appointments;
        }
        catch (HttpRequestException)
        {
            var cached = await _cache.GetAllAsync<AppointmentSummary>(cancellationToken).ConfigureAwait(false);
            return cached.Where(appointment => appointment.Customer.Id == customerId).ToList();
        }
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"vehicles/{vehicleId}/appointments", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<AppointmentSummary>();
            foreach (var appointment in appointments)
            {
                await _cache.SetAsync(appointment.Id.ToString(), appointment, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return appointments;
        }
        catch (HttpRequestException)
        {
            var cached = await _cache.GetAllAsync<AppointmentSummary>(cancellationToken).ConfigureAwait(false);
            return cached.Where(appointment => appointment.Vehicle.Id == vehicleId).ToList();
        }
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetUpcomingAppointmentsAsync(int count, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync($"appointments/upcoming?count={count}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentSummary>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<AppointmentSummary>();
            foreach (var appointment in appointments)
            {
                await _cache.SetAsync(appointment.Id.ToString(), appointment, cancellationToken).ConfigureAwait(false);
            }

            _syncState.SetOffline(false);
            return appointments;
        }
        catch (HttpRequestException)
        {
            var cached = await _cache.GetAllAsync<AppointmentSummary>(cancellationToken).ConfigureAwait(false);
            return cached.Where(appointment => appointment.ScheduledStart >= DateTime.UtcNow)
                .OrderBy(appointment => appointment.ScheduledStart)
                .Take(count)
                .ToList();
        }
    }

    public async Task<IReadOnlyList<string>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Client.GetAsync("appointments/statuses", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var statuses = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? new List<string>();
            _syncState.SetOffline(false);
            return statuses;
        }
        catch (HttpRequestException)
        {
            var cached = await _cache.GetAllAsync<AppointmentSummary>(cancellationToken).ConfigureAwait(false);
            return cached.Select(appointment => appointment.Status).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(status => status).ToList();
        }
    }

    public async Task<AppointmentDetail> SaveAppointmentAsync(AppointmentDetail appointment, CancellationToken cancellationToken = default)
    {
        if (appointment is null)
        {
            throw new ArgumentNullException(nameof(appointment));
        }

        var target = appointment.Id == Guid.Empty ? appointment with { Id = Guid.NewGuid() } : appointment;
        var isNew = appointment.Id == Guid.Empty;

        try
        {
            var route = isNew ? "appointments" : $"appointments/{target.Id}";
            var method = isNew ? HttpMethod.Post : HttpMethod.Put;
            using var request = new HttpRequestMessage(method, route)
            {
                Content = JsonContent.Create(target)
            };

            var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var saved = await response.Content.ReadFromJsonAsync<AppointmentDetail>(cancellationToken: cancellationToken).ConfigureAwait(false) ?? target;
            await CacheAppointmentAsync(saved, cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(false);
            return saved;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Queueing appointment {Operation} for {AppointmentId} due to API failure.", isNew ? "create" : "update", target.Id);
            var envelope = isNew
                ? ChangeEnvelope.ForCreate("Appointments", target.Id.ToString(), target)
                : ChangeEnvelope.ForUpdate("Appointments", target.Id.ToString(), target);
            await _syncQueue.EnqueueChangeAsync(envelope, cancellationToken).ConfigureAwait(false);
            await CacheAppointmentAsync(target, cancellationToken).ConfigureAwait(false);
            _syncState.SetOffline(true);
            return target;
        }
    }

    private static string BuildRangeQuery(DateTime? start, DateTime? end, string? status)
    {
        var parameters = new List<string>();
        if (start.HasValue)
        {
            parameters.Add($"start={Uri.EscapeDataString(start.Value.ToString("o"))}");
        }

        if (end.HasValue)
        {
            parameters.Add($"end={Uri.EscapeDataString(end.Value.ToString("o"))}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            parameters.Add($"status={Uri.EscapeDataString(status)}");
        }

        return parameters.Count == 0 ? string.Empty : "?" + string.Join("&", parameters);
    }

    private IReadOnlyList<AppointmentSummary> FilterAppointments(IReadOnlyList<AppointmentSummary> appointments, DateTime? start, DateTime? end, string? status)
    {
        IEnumerable<AppointmentSummary> query = appointments;
        if (start.HasValue)
        {
            query = query.Where(appointment => appointment.ScheduledStart >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(appointment => appointment.ScheduledEnd <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(appointment => string.Equals(appointment.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(appointment => appointment.ScheduledStart).ToList();
    }

    private async Task CacheAppointmentAsync(AppointmentDetail detail, CancellationToken cancellationToken)
    {
        await _cache.SetAsync(detail.Id.ToString(), detail, cancellationToken).ConfigureAwait(false);
        var summary = BuildSummary(detail);
        await _cache.SetAsync(summary.Id.ToString(), summary, cancellationToken).ConfigureAwait(false);
    }

    private static AppointmentSummary BuildSummary(AppointmentDetail detail)
    {
        var notesPreview = string.IsNullOrWhiteSpace(detail.Notes)
            ? null
            : detail.Notes.Length > 120
                ? detail.Notes[..120] + "…"
                : detail.Notes;

        return new AppointmentSummary(
            detail.Id,
            detail.AppointmentNumber,
            detail.ScheduledStart,
            detail.ScheduledEnd,
            detail.Status,
            detail.Service,
            detail.Technician,
            detail.Customer,
            detail.Vehicle,
            notesPreview);
    }
}
