// RecentActivityItem.cs: Captures recent CRM interactions across invoices, appointments, and customers.
using System;

namespace CRMAdapter.UI.Services.Dashboard.Models;

public sealed record RecentActivityItem(
    DateTime OccurredOn,
    string Type,
    string Title,
    string Description,
    string Url);
