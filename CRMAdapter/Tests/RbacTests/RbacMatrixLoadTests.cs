// RbacMatrixLoadTests.cs: Validates that the RBAC matrix loads and exposes expected assignments.
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CRMAdapter.Tests.RbacTests;

public sealed class RbacMatrixLoadTests
{
    [Fact]
    public async Task LoadAsync_ResolvesRoleAndActionMappings()
    {
        var environment = TestHostEnvironment.Create("CRMAdapter.Api");
        var matrix = await RbacPolicy.LoadAsync(environment).ConfigureAwait(false);

        var adminActions = matrix.GetActionsForRole(RbacRole.Admin);
        adminActions.Should().Contain(RbacAction.InvoiceExport);

        var invoiceRoles = matrix.GetRolesForAction(RbacAction.InvoiceView);
        invoiceRoles.Should().Contain(RbacRole.Clerk);
        invoiceRoles.Should().NotContain(RbacRole.Tech);
    }
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    private TestHostEnvironment(string contentRoot)
    {
        ContentRootPath = contentRoot;
        ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
    }

    public string ApplicationName { get; set; } = "CRMAdapter.Tests";

    public IFileProvider ContentRootFileProvider { get; set; }

    public string ContentRootPath { get; set; }

    public string EnvironmentName { get; set; } = Environments.Development;

    public static IHostEnvironment Create(string projectFolder)
    {
        var solutionRoot = ResolveSolutionRoot();
        var contentRoot = Path.Combine(solutionRoot, projectFolder);
        return new TestHostEnvironment(contentRoot);
    }

    private static string ResolveSolutionRoot()
    {
        var current = AppContext.BaseDirectory;
        var probe = Path.GetFullPath(Path.Combine(current, "..", "..", "..", ".."));
        if (File.Exists(Path.Combine(probe, "CRMAdapter.sln")))
        {
            return probe;
        }

        throw new DirectoryNotFoundException("Unable to locate solution root for RBAC tests.");
    }
}
