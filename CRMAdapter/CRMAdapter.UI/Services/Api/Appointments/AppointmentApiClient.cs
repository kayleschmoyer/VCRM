// AppointmentApiClient.cs: HTTP client facade for appointment operations, currently serving mock data.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Api;
using CRMAdapter.UI.Services.Appointments.Models;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Mock.Appointments;

namespace CRMAdapter.UI.Services.Api.Appointments;

public sealed class AppointmentApiClient : BaseApiClient, IAppointmentService
{
    private readonly InMemoryAppointmentBook _mock;

    public AppointmentApiClient(HttpClient client, InMemoryAppointmentBook mock)
        : base(client)
    {
        _mock = mock;
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsAsync(
        DateTime? start = null,
        DateTime? end = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/appointments query once backend is wired.
        return await _mock.GetAppointmentsAsync(start, end, status, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppointmentDetail?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/appointments/{appointmentId} once backend is wired.
        return await _mock.GetAppointmentAsync(appointmentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/customers/{customerId}/appointments once backend is wired.
        return await _mock.GetAppointmentsForCustomerAsync(customerId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/vehicles/{vehicleId}/appointments once backend is wired.
        return await _mock.GetAppointmentsForVehicleAsync(vehicleId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AppointmentSummary>> GetUpcomingAppointmentsAsync(int count, CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/appointments/upcoming once backend is wired.
        return await _mock.GetUpcomingAppointmentsAsync(count, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Replace with GET /api/appointments/statuses once backend is wired.
        return await _mock.GetStatusesAsync(cancellationToken).ConfigureAwait(false);
    }
}
