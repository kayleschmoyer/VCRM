// RbacPolicy.cs: Centralized RBAC primitives, policy registration helpers, and runtime authorization utilities.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Enumerates the canonical CRM Adapter roles supported by the RBAC system.
/// </summary>
public enum RbacRole
{
    /// <summary>
    /// Represents the administrator persona with unrestricted access.
    /// </summary>
    Admin,

    /// <summary>
    /// Represents managerial personas with broad operational privileges.
    /// </summary>
    Manager,

    /// <summary>
    /// Represents front-office clerks focused on customer operations.
    /// </summary>
    Clerk,

    /// <summary>
    /// Represents field technicians with service delivery responsibilities.
    /// </summary>
    Tech,
}

/// <summary>
/// Enumerates discrete RBAC actions that can be protected via authorization policies.
/// </summary>
public enum RbacAction
{
    /// <summary>
    /// Allows access to dashboard analytics experiences.
    /// </summary>
    [EnumMember(Value = "Dashboard.View")]
    DashboardView,

    /// <summary>
    /// Allows reading canonical customer records.
    /// </summary>
    [EnumMember(Value = "Customer.View")]
    CustomerView,

    /// <summary>
    /// Allows editing canonical customer records.
    /// </summary>
    [EnumMember(Value = "Customer.Edit")]
    CustomerEdit,

    /// <summary>
    /// Allows executing customer search queries.
    /// </summary>
    [EnumMember(Value = "Customer.Search")]
    CustomerSearch,

    /// <summary>
    /// Allows reading canonical appointment details.
    /// </summary>
    [EnumMember(Value = "Appointment.View")]
    AppointmentView,

    /// <summary>
    /// Allows reading canonical invoice details.
    /// </summary>
    [EnumMember(Value = "Invoice.View")]
    InvoiceView,

    /// <summary>
    /// Allows exporting invoice data sets.
    /// </summary>
    [EnumMember(Value = "Invoice.Export")]
    InvoiceExport,

    /// <summary>
    /// Allows reading canonical vehicle details.
    /// </summary>
    [EnumMember(Value = "Vehicle.View")]
    VehicleView,

    /// <summary>
    /// Allows accessing operations orchestration tooling.
    /// </summary>
    [EnumMember(Value = "Operations.View")]
    OperationsView,

    /// <summary>
    /// Allows participating in field service workflows.
    /// </summary>
    [EnumMember(Value = "FieldService.View")]
    FieldServiceView,

    /// <summary>
    /// Allows accessing data quality stewardship tooling.
    /// </summary>
    [EnumMember(Value = "DataQuality.View")]
    DataQualityView,

    /// <summary>
    /// Allows accessing the support desk workspace.
    /// </summary>
    [EnumMember(Value = "Support.View")]
    SupportView,
}

/// <summary>
/// Represents the resolved RBAC matrix mapping roles to actions and vice versa.
/// </summary>
public sealed class RbacMatrix
{
    private readonly IReadOnlyDictionary<RbacRole, IReadOnlyCollection<RbacAction>> _roleAssignments;
    private readonly IReadOnlyDictionary<RbacAction, IReadOnlyCollection<RbacRole>> _actionAssignments;

    /// <summary>
    /// Initializes a new instance of the <see cref="RbacMatrix"/> class.
    /// </summary>
    /// <param name="roleAssignments">Role to action mapping sourced from configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="roleAssignments"/> is <c>null</c>.</exception>
    public RbacMatrix(IDictionary<RbacRole, IReadOnlyCollection<RbacAction>> roleAssignments)
    {
        ArgumentNullException.ThrowIfNull(roleAssignments);

        _roleAssignments = roleAssignments.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyCollection<RbacAction>)new ReadOnlyCollection<RbacAction>(pair.Value.Distinct().ToList()));

        var actionMap = new Dictionary<RbacAction, HashSet<RbacRole>>();
        foreach (var (role, actions) in _roleAssignments)
        {
            foreach (var action in actions)
            {
                if (!actionMap.TryGetValue(action, out var set))
                {
                    set = new HashSet<RbacRole>();
                    actionMap[action] = set;
                }

                set.Add(role);
            }
        }

        _actionAssignments = actionMap.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyCollection<RbacRole>)new ReadOnlyCollection<RbacRole>(pair.Value.Order().ToList()));
    }

    /// <summary>
    /// Retrieves the allowed actions for the specified role.
    /// </summary>
    /// <param name="role">Role whose permissions should be resolved.</param>
    /// <returns>A read-only collection of actions granted to the role.</returns>
    public IReadOnlyCollection<RbacAction> GetActionsForRole(RbacRole role)
    {
        return _roleAssignments.TryGetValue(role, out var actions)
            ? actions
            : Array.Empty<RbacAction>();
    }

    /// <summary>
    /// Retrieves the allowed roles for the specified action.
    /// </summary>
    /// <param name="action">Action whose permitted roles should be resolved.</param>
    /// <returns>A read-only collection of roles authorized for the action.</returns>
    public IReadOnlyCollection<RbacRole> GetRolesForAction(RbacAction action)
    {
        return _actionAssignments.TryGetValue(action, out var roles)
            ? roles
            : Array.Empty<RbacRole>();
    }

    /// <summary>
    /// Determines whether the specified role has access to the supplied action.
    /// </summary>
    /// <param name="role">Role to evaluate.</param>
    /// <param name="action">Action to evaluate.</param>
    /// <returns><c>true</c> when the role is granted access; otherwise, <c>false</c>.</returns>
    public bool IsRoleAuthorized(RbacRole role, RbacAction action)
    {
        return GetRolesForAction(action).Contains(role);
    }
}

/// <summary>
/// Provides helpers for loading RBAC configuration and registering ASP.NET Core authorization policies.
/// </summary>
public static class RbacPolicy
{
    private const string MatrixFileName = "RbacMatrix.json";
    private const string MatrixDirectoryName = "CommonConfig";
    private static readonly IReadOnlyDictionary<RbacAction, string> ActionNames = BuildActionNameMap();
    private static readonly IReadOnlyDictionary<string, RbacAction> ActionLookup = ActionNames.ToDictionary(
        static pair => pair.Value,
        static pair => pair.Key,
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads the RBAC matrix from the shared configuration directory.
    /// </summary>
    /// <param name="environment">Host environment used to locate the configuration file.</param>
    /// <param name="cancellationToken">Cancellation token to observe while loading the matrix.</param>
    /// <returns>A task that resolves to the parsed <see cref="RbacMatrix"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the RBAC matrix file cannot be located.</exception>
    /// <exception cref="InvalidDataException">Thrown when the RBAC matrix file contains invalid role or action entries.</exception>
    public static async Task<RbacMatrix> LoadAsync(IHostEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var matrixPath = ResolveMatrixPath(environment.ContentRootPath);
        if (!File.Exists(matrixPath))
        {
            throw new FileNotFoundException($"RBAC matrix file not found at '{matrixPath}'.", matrixPath);
        }

        await using var stream = File.OpenRead(matrixPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("RBAC matrix must be a JSON object mapping roles to actions.");
        }

        var rolesElement = root.TryGetProperty("roles", out var nestedRoles) ? nestedRoles : root;
        if (rolesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("RBAC matrix must contain a 'roles' object with role assignments.");
        }

        var assignments = new Dictionary<RbacRole, IReadOnlyCollection<RbacAction>>();
        foreach (var property in rolesElement.EnumerateObject())
        {
            if (!Enum.TryParse<RbacRole>(property.Name, ignoreCase: false, out var role))
            {
                throw new InvalidDataException($"Unknown role '{property.Name}' in RBAC matrix.");
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Role '{property.Name}' must contain an array of actions.");
            }

            var actions = new List<RbacAction>();
            foreach (var actionElement in property.Value.EnumerateArray())
            {
                if (actionElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"Role '{property.Name}' includes a non-string action entry.");
                }

                var actionName = actionElement.GetString();
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    throw new InvalidDataException($"Role '{property.Name}' contains an empty action entry.");
                }

                if (!TryParseAction(actionName, out var action))
                {
                    throw new InvalidDataException($"Role '{property.Name}' references unknown action '{actionName}'.");
                }

                actions.Add(action);
            }

            assignments[role] = new ReadOnlyCollection<RbacAction>(actions.Distinct().ToList());
        }

        return new RbacMatrix(assignments);
    }

    /// <summary>
    /// Registers ASP.NET Core authorization policies for the supplied RBAC matrix.
    /// </summary>
    /// <param name="options">Authorization options used to register policies.</param>
    /// <param name="matrix">RBAC matrix containing the role assignments.</param>
    public static void RegisterPolicies(AuthorizationOptions options, RbacMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(matrix);

        foreach (var action in ActionNames.Keys)
        {
            var policyName = GetPolicyName(action);
            var roles = matrix.GetRolesForAction(action);
            if (roles.Count == 0)
            {
                continue;
            }

            options.AddPolicy(policyName, policy =>
            {
                var roleNames = roles.Select(static role => role.ToString()).ToArray();
                policy.RequireRole(roleNames);
            });
        }
    }

    /// <summary>
    /// Resolves the policy name associated with the supplied RBAC action.
    /// </summary>
    /// <param name="action">Action whose policy name is requested.</param>
    /// <returns>The normalized policy name used with authorization attributes.</returns>
    public static string GetPolicyName(RbacAction action)
    {
        return ActionNames[action];
    }

    /// <summary>
    /// Determines whether the supplied user satisfies the RBAC policy for the given action.
    /// </summary>
    /// <param name="user">Authenticated principal to evaluate.</param>
    /// <param name="matrix">RBAC matrix containing the role assignments.</param>
    /// <param name="action">Action to evaluate.</param>
    /// <returns><c>true</c> when the user satisfies the policy; otherwise, <c>false</c>.</returns>
    public static bool IsAuthorized(ClaimsPrincipal? user, RbacMatrix matrix, RbacAction action)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userRoles = user.FindAll(ClaimTypes.Role).Select(static claim => claim.Value).Where(static value => !string.IsNullOrWhiteSpace(value));
        var authorizedRoles = new HashSet<string>(matrix.GetRolesForAction(action).Select(static role => role.ToString()), StringComparer.OrdinalIgnoreCase);
        return userRoles.Any(authorizedRoles.Contains);
    }

    private static bool TryParseAction(string actionName, out RbacAction action)
    {
        return ActionLookup.TryGetValue(actionName, out action);
    }

    private static string ResolveMatrixPath(string contentRootPath)
    {
        var basePath = string.IsNullOrWhiteSpace(contentRootPath) ? Directory.GetCurrentDirectory() : contentRootPath;
        var combined = Path.Combine(basePath, "..", MatrixDirectoryName, MatrixFileName);
        return Path.GetFullPath(combined);
    }

    private static IReadOnlyDictionary<RbacAction, string> BuildActionNameMap()
    {
        var pairs = new Dictionary<RbacAction, string>();
        foreach (var action in Enum.GetValues<RbacAction>())
        {
            var name = ResolveEnumMemberValue(action);
            pairs[action] = name;
        }

        return new ReadOnlyDictionary<RbacAction, string>(pairs);
    }

    private static string ResolveEnumMemberValue<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString());
        if (member.Length == 0)
        {
            return value.ToString();
        }

        var attribute = member[0].GetCustomAttributes(typeof(EnumMemberAttribute), false).OfType<EnumMemberAttribute>().FirstOrDefault();
        return attribute?.Value ?? value.ToString();
    }
}

/// <summary>
/// Exposes compile-time constants for well-known RBAC policy names.
/// </summary>
public static class RbacPolicyNames
{
    public const string DashboardView = "Dashboard.View";
    public const string CustomerView = "Customer.View";
    public const string CustomerEdit = "Customer.Edit";
    public const string CustomerSearch = "Customer.Search";
    public const string AppointmentView = "Appointment.View";
    public const string InvoiceView = "Invoice.View";
    public const string InvoiceExport = "Invoice.Export";
    public const string VehicleView = "Vehicle.View";
    public const string OperationsView = "Operations.View";
    public const string FieldServiceView = "FieldService.View";
    public const string DataQualityView = "DataQuality.View";
    public const string SupportView = "Support.View";
}

/// <summary>
/// Defines a reusable contract for evaluating RBAC policies against principals.
/// </summary>
public interface IRbacAuthorizationService
{
    /// <summary>
    /// Determines whether the supplied principal may perform the requested action.
    /// </summary>
    /// <param name="user">Principal to evaluate.</param>
    /// <param name="action">Action to evaluate.</param>
    /// <returns><c>true</c> when access is granted; otherwise, <c>false</c>.</returns>
    bool HasAccess(ClaimsPrincipal? user, RbacAction action);
}

/// <summary>
/// Default implementation of <see cref="IRbacAuthorizationService"/> backed by the configured RBAC matrix.
/// </summary>
public sealed class RbacAuthorizationService : IRbacAuthorizationService
{
    private readonly RbacMatrix _matrix;

    /// <summary>
    /// Initializes a new instance of the <see cref="RbacAuthorizationService"/> class.
    /// </summary>
    /// <param name="matrix">RBAC matrix used for authorization decisions.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <c>null</c>.</exception>
    public RbacAuthorizationService(RbacMatrix matrix)
    {
        _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
    }

    /// <inheritdoc />
    public bool HasAccess(ClaimsPrincipal? user, RbacAction action)
    {
        return RbacPolicy.IsAuthorized(user, _matrix, action);
    }
}
