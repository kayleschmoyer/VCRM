// NavigationMenuService.cs: Supplies role-trimmed navigation items and active route utilities for the layout.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using CRMAdapter.UI.Auth;

namespace CRMAdapter.UI.Navigation;

public sealed class NavigationMenuService
{
    private static readonly IReadOnlyList<NavigationLink> Links = new List<NavigationLink>
    {
        new("Dashboard", Icons.Material.Filled.Insights, "/dashboard", "Executive overview with actionable KPIs.", new[] { RolePolicies.Admin, RolePolicies.Manager, RolePolicies.Clerk }),
        new("Customers", Icons.Material.Filled.PeopleAlt, "/customers", "360° customer directory and relationship insights.", RolePolicies.AllRoles),
        new("Appointments", Icons.Material.Filled.EventAvailable, "/appointments", "Scheduling, technician dispatch, and service visibility.", RolePolicies.AllRoles),
        new("Invoices", Icons.Material.Filled.ReceiptLong, "/invoices", "Billing performance, balances, and payment activity.", new[] { RolePolicies.Admin, RolePolicies.Manager, RolePolicies.Clerk }),
        new("Vehicles", Icons.Material.Filled.DirectionsCar, "/vehicles", "Unified fleet management and service visibility.", RolePolicies.AllRoles),
        new("Operations", Icons.Material.Filled.PrecisionManufacturing, "/operations", "Order orchestration and fulfillment visibility.", new[] { RolePolicies.Admin, RolePolicies.Manager, RolePolicies.Clerk }),
        new("Field Service", Icons.Material.Filled.HomeRepairService, "/field-service", "Technician dispatching and status intelligence.", new[] { RolePolicies.Tech, RolePolicies.Manager }),
        new("Data Stewardship", Icons.Material.Filled.Storage, "/data-quality", "Data quality dashboards and stewardship workflows.", new[] { RolePolicies.Admin, RolePolicies.Manager }),
        new("Support Desk", Icons.Material.Filled.SupportAgent, "/support", "Customer support command console and SLA tracking.", new[] { RolePolicies.Admin, RolePolicies.Clerk })
    };

    public IEnumerable<NavigationLink> GetLinksForUser(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Links.Where(l => l.AllowedRoles.Length == 0);
        }

        return Links.Where(link => link.AllowedRoles.Length == 0 || link.AllowedRoles.Any(user.IsInRole)).ToArray();
    }

    public bool IsActive(NavigationManager navigationManager, NavigationLink link)
    {
        var currentUri = navigationManager.Uri;
        var absoluteLink = navigationManager.ToAbsoluteUri(link.Href).ToString();
        return string.Equals(currentUri, absoluteLink, StringComparison.OrdinalIgnoreCase) || currentUri.StartsWith(absoluteLink, StringComparison.OrdinalIgnoreCase);
    }
}
