// UiRoleVisibilityTests.cs: Ensures navigation elements respect RBAC visibility rules.
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using CRMAdapter.UI.Navigation;
using FluentAssertions;
using Xunit;

namespace CRMAdapter.Tests.RbacTests;

public sealed class UiRoleVisibilityTests
{
    [Fact]
    public async Task NavigationForTechnician_ExcludesSupportDesk()
    {
        var matrix = await LoadMatrixAsync().ConfigureAwait(false);
        var service = new NavigationMenuService(new RbacAuthorizationService(matrix));
        var technician = CreatePrincipal(RbacRole.Tech);

        var links = service.GetLinksForUser(technician).ToList();

        links.Should().Contain(link => link.Title == "Field Service");
        links.Should().NotContain(link => link.Title == "Support Desk");
    }

    [Fact]
    public async Task NavigationForManager_IncludesDataQuality()
    {
        var matrix = await LoadMatrixAsync().ConfigureAwait(false);
        var service = new NavigationMenuService(new RbacAuthorizationService(matrix));
        var manager = CreatePrincipal(RbacRole.Manager);

        var links = service.GetLinksForUser(manager).ToList();

        links.Should().Contain(link => link.Title == "Data Stewardship");
    }

    private static async Task<RbacMatrix> LoadMatrixAsync()
    {
        var environment = TestHostEnvironment.Create("CRMAdapter.UI");
        return await RbacPolicy.LoadAsync(environment).ConfigureAwait(false);
    }

    private static ClaimsPrincipal CreatePrincipal(RbacRole role)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role.ToString()) }, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }
}
