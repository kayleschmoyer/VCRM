// UiAuditFlowTests.cs: End-to-end style validation of UI to API audit event propagation.
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bunit;
using CRMAdapter.Api.Middleware;
using CRMAdapter.CommonSecurity;
using CRMAdapter.UI.Components.Audit;
using CRMAdapter.UI.Services.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMAdapter.Tests.AuditTests;

public sealed class UiAuditFlowTests : IDisposable
{
    private readonly TestContext _testContext;
    private readonly TestAuditSink _sink;

    public UiAuditFlowTests()
    {
        _testContext = new TestContext();
        _sink = new TestAuditSink();

        _testContext.Services.AddLogging();
        _testContext.Services.AddSingleton(_sink);
        _testContext.Services.AddSingleton<IAuditSink>(_sink);
        _testContext.Services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AuditLogger>>();
            var sinks = new[] { sp.GetRequiredService<IAuditSink>() };
            return new AuditLogger(sinks, logger);
        });
        _testContext.Services.AddSingleton(new CorrelationContext());
        _testContext.Services.AddSingleton<AuthenticationStateProvider>(new StaticAuthStateProvider());
    }

    [Fact]
    public async Task ClickingAuditedAction_ShouldEmitUiAndApiEventsWithSharedCorrelation()
    {
        // Arrange
        var correlationContext = _testContext.Services.GetRequiredService<CorrelationContext>();
        correlationContext.SetCorrelationId(Guid.NewGuid().ToString("N"));
        var auditLogger = _testContext.Services.GetRequiredService<AuditLogger>();
        var middleware = new AuditMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            auditLogger,
            NullLogger<AuditMiddleware>.Instance);

        var entityId = Guid.NewGuid().ToString("N");

        var component = _testContext.RenderComponent<AuditActionInvoker>(parameters => parameters
            .Add(p => p.Action, "Invoice.Export")
            .Add(p => p.EntityId, entityId)
            .Add(p => p.OnExecute, EventCallback.Factory.Create(this, async () =>
            {
                var httpContext = new DefaultHttpContext
                {
                    TraceIdentifier = correlationContext.CurrentCorrelationId,
                };
                httpContext.Items[CorrelationIdMiddleware.CorrelationHeaderName] = correlationContext.CurrentCorrelationId;
                httpContext.Request.Method = HttpMethods.Post;
                httpContext.Request.Path = "/invoices/export";
                httpContext.Request.RouteValues["invoiceId"] = entityId;
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                await middleware.InvokeAsync(httpContext);
            }))
            .Add(p => p.ChildContent, callback => builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "type", "button");
                builder.AddAttribute(2, "onclick", callback);
                builder.AddContent(3, "Export");
                builder.CloseElement();
            }));

        // Act
        await component.Find("button").ClickAsync();

        // Assert
        var events = _sink.Events;
        events.Should().Contain(e => e.Action == "Invoice.Export.Attempt" && e.Metadata is { } meta && meta.TryGetValue("origin", out var origin) && origin == "UI");
        events.Should().Contain(e => e.Action == "Invoice.Export" && e.Metadata is { } meta && meta.TryGetValue("origin", out var origin) && origin == "UI" && e.Result == AuditResult.Success);
        events.Should().Contain(e => e.Action == "Invoice.Export.Attempt" && e.Metadata is { } meta && meta.TryGetValue("origin", out var origin) && origin == "API");
        events.Should().Contain(e => e.Action == "Invoice.Export" && e.Metadata is { } meta && meta.TryGetValue("origin", out var origin) && origin == "API");
        events.Select(e => e.CorrelationId).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task ClickingAuditedAction_WhenCallbackThrows_ShouldLogFailure()
    {
        // Arrange
        var correlationContext = _testContext.Services.GetRequiredService<CorrelationContext>();
        correlationContext.SetCorrelationId(Guid.NewGuid().ToString("N"));

        var component = _testContext.RenderComponent<AuditActionInvoker>(parameters => parameters
            .Add(p => p.Action, "Invoice.Download")
            .Add(p => p.EntityId, Guid.NewGuid().ToString("N"))
            .Add(p => p.OnExecute, EventCallback.Factory.Create(this, async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("Simulated failure");
            }))
            .Add(p => p.ChildContent, callback => builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "type", "button");
                builder.AddAttribute(2, "onclick", callback);
                builder.AddContent(3, "Download");
                builder.CloseElement();
            }));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await component.Find("button").ClickAsync());

        // Assert
        _sink.Events.Should().Contain(e => e.Action == "Invoice.Download" && e.Result == AuditResult.Failure && e.Metadata is { } meta && meta.TryGetValue("origin", out var origin) && origin == "UI");
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    private sealed class StaticAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _principal = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-12345"),
            new Claim(ClaimTypes.Role, "Finance"),
        }, authenticationType: "test"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(_principal));
        }
    }
}
