// IAppointmentService.cs: Stable contract for querying appointment timelines and summaries across providers.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Services.Appointments.Models;

namespace CRMAdapter.UI.Services.Contracts;

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsAsync(
        DateTime? start = null,
        DateTime? end = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<AppointmentDetail?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentSummary>> GetAppointmentsForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentSummary>> GetUpcomingAppointmentsAsync(int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetStatusesAsync(CancellationToken cancellationToken = default);

    Task<AppointmentDetail> SaveAppointmentAsync(AppointmentDetail appointment, CancellationToken cancellationToken = default);
}
