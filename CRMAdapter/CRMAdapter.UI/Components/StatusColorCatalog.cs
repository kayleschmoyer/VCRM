// StatusColorCatalog.cs: Centralizes mapping of lifecycle statuses to MudBlazor chip colors.
using MudBlazor;

namespace CRMAdapter.UI.Components;

public static class StatusColorCatalog
{
    public static Color Resolve(string status)
    {
        return status switch
        {
            "Paid" or "Active" or "Completed" => Color.Success,
            "Processing" or "Scheduled" or "In service" or "In diagnostics" => Color.Info,
            "Draft" or "Awaiting parts" or "In progress" => Color.Warning,
            "Past due" or "Overdue" or "Retired" or "Canceled" => Color.Error,
            _ => Color.Default
        };
    }
}
