#nullable enable
using System;
using CRMAdapter.CommonInfrastructure;
using FluentAssertions;
using Xunit;

namespace CRMAdapter.Tests.UnitTests.CommonInfrastructure;

public class AdapterCorrelationScopeTests
{
    [Fact]
    public void BeginScope_AssignsCorrelationIdentifier()
    {
        using var scope = AdapterCorrelationScope.BeginScope();
        scope.CorrelationId.Should().NotBeNullOrEmpty();
        AdapterCorrelationScope.CurrentCorrelationId.Should().Be(scope.CorrelationId);
    }

    [Fact]
    public void DisposeScope_RestoresPreviousCorrelationId()
    {
        using var outer = AdapterCorrelationScope.BeginScope("outer");
        using (var inner = AdapterCorrelationScope.BeginScope())
        {
            inner.CorrelationId.Should().Be("outer");
            AdapterCorrelationScope.CurrentCorrelationId.Should().Be("outer");
        }

        AdapterCorrelationScope.CurrentCorrelationId.Should().Be("outer");
    }

    [Fact]
    public void NestedScopes_WithExplicitIdentifier_OverrideAmbient()
    {
        using var outer = AdapterCorrelationScope.BeginScope("outer");
        using (var inner = AdapterCorrelationScope.BeginScope("inner"))
        {
            inner.CorrelationId.Should().Be("inner");
            AdapterCorrelationScope.CurrentCorrelationId.Should().Be("inner");
        }

        AdapterCorrelationScope.CurrentCorrelationId.Should().Be("outer");
    }
}
