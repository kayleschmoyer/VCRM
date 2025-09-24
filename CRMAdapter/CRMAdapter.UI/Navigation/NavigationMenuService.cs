// NavigationMenuService.cs: Supplies RBAC-trimmed navigation items and active route utilities for the layout.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using CRMAdapter.CommonSecurity;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CRMAdapter.UI.Navigation;

public sealed class NavigationMenuService
{
    private static readonly IReadOnlyList<NavigationLink> Links = new List<NavigationLink>
    {
        new("Dashboard", Icons.Material.Filled.Insights, "/dashboard", "Executive overview with actionable KPIs.", RbacAction.DashboardView),
        new("Customers", Icons.Material.Filled.PeopleAlt, "/customers", "360° customer directory and relationship insights.", RbacAction.CustomerView),
        new("Appointments", Icons.Material.Filled.EventAvailable, "/appointments", "Scheduling, technician dispatch, and service visibility.", RbacAction.AppointmentView),
        new("Invoices", Icons.Material.Filled.ReceiptLong, "/invoices", "Billing performance, balances, and payment activity.", RbacAction.InvoiceView),
        new("Vehicles", Icons.Material.Filled.DirectionsCar, "/vehicles", "Unified fleet management and service visibility.", RbacAction.VehicleView),
        new("Operations", Icons.Material.Filled.PrecisionManufacturing, "/operations", "Order orchestration and fulfillment visibility.", RbacAction.OperationsView),
        new("Field Service", Icons.Material.Filled.HomeRepairService, "/field-service", "Technician dispatching and status intelligence.", RbacAction.FieldServiceView),
        new("Data Stewardship", Icons.Material.Filled.Storage, "/data-quality", "Data quality dashboards and stewardship workflows.", RbacAction.DataQualityView),
        new("Support Desk", Icons.Material.Filled.SupportAgent, "/support", "Customer support command console and SLA tracking.", RbacAction.SupportView)
    };

    private readonly IRbacAuthorizationService _authorizationService;

    public NavigationMenuService(IRbacAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    public IEnumerable<NavigationLink> GetLinksForUser(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<NavigationLink>();
        }

        return Links.Where(link => _authorizationService.HasAccess(user, link.RequiredAction)).ToArray();
    }

    public bool IsActive(NavigationManager navigationManager, NavigationLink link)
    {
        var currentUri = navigationManager.Uri;
        var absoluteLink = navigationManager.ToAbsoluteUri(link.Href).ToString();
        return string.Equals(currentUri, absoluteLink, StringComparison.OrdinalIgnoreCase) || currentUri.StartsWith(absoluteLink, StringComparison.OrdinalIgnoreCase);
    }
}
