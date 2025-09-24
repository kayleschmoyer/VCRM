// RolePolicies.cs: Declares CRMAdapter roles and registers authorization policies for consistent enforcement.
using System;
using Microsoft.AspNetCore.Authorization;

namespace CRMAdapter.UI.Auth;

public static class RolePolicies
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Clerk = "Clerk";
    public const string Tech = "Tech";

    public static readonly string[] AllRoles = { Admin, Manager, Clerk, Tech };

    public static void RegisterPolicies(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(Admin, policy => policy.RequireRole(Admin));
        options.AddPolicy(Manager, policy => policy.RequireRole(Manager, Admin));
        options.AddPolicy(Clerk, policy => policy.RequireRole(Clerk, Manager, Admin));
        options.AddPolicy(Tech, policy => policy.RequireRole(Tech, Manager, Admin));
    }
}
