// File: AuthPolicies.cs
// Summary: Centralizes role-based access control policy definitions for the API.
using System;
using Microsoft.AspNetCore.Authorization;

namespace CRMAdapter.Api.Security;

/// <summary>
/// Defines authorization policy names and role assignments used by the API.
/// </summary>
public static class AuthPolicies
{
    /// <summary>
    /// Policy requiring roles that can read customer information.
    /// </summary>
    public const string CustomerRead = "Policies.Customers.Read";

    /// <summary>
    /// Policy requiring roles that can execute customer search operations.
    /// </summary>
    public const string CustomerSearch = "Policies.Customers.Search";

    /// <summary>
    /// Policy requiring roles that can read vehicle information.
    /// </summary>
    public const string VehicleRead = "Policies.Vehicles.Read";

    /// <summary>
    /// Policy requiring roles that can read invoice information.
    /// </summary>
    public const string InvoiceRead = "Policies.Invoices.Read";

    /// <summary>
    /// Policy requiring roles that can read appointment information.
    /// </summary>
    public const string AppointmentRead = "Policies.Appointments.Read";

    /// <summary>
    /// Registers authorization policies with the supplied options.
    /// </summary>
    /// <param name="options">Authorization options to populate.</param>
    public static void Configure(AuthorizationOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.AddPolicy(CustomerRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Clerk));
        options.AddPolicy(CustomerSearch, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Clerk));
        options.AddPolicy(VehicleRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Tech));
        options.AddPolicy(InvoiceRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Clerk));
        options.AddPolicy(AppointmentRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Tech, Roles.Clerk));
    }

    /// <summary>
    /// Declares canonical role names used by the API policies.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// Gets the administrator role name.
        /// </summary>
        public const string Admin = "Admin";

        /// <summary>
        /// Gets the manager role name.
        /// </summary>
        public const string Manager = "Manager";

        /// <summary>
        /// Gets the clerk role name.
        /// </summary>
        public const string Clerk = "Clerk";

        /// <summary>
        /// Gets the technician role name.
        /// </summary>
        public const string Tech = "Tech";
    }
}
