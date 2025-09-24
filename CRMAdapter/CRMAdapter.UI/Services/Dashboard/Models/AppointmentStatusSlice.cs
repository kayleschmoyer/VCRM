// AppointmentStatusSlice.cs: Tracks appointment counts by status for pie chart visualization.

namespace CRMAdapter.UI.Services.Dashboard.Models;

public sealed record AppointmentStatusSlice(
    string Status,
    int Count);
