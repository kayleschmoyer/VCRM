// ApiAuditMiddlewareTests.cs: Integration-style tests covering API audit middleware outcomes.
using System;
using System.Threading.Tasks;
using CRMAdapter.Api.Middleware;
using CRMAdapter.CommonSecurity;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CRMAdapter.Tests.AuditTests;

public sealed class ApiAuditMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenAuthorizationDenies_ShouldLogDeniedResult()
    {
        // Arrange
        var sink = new TestAuditSink();
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        var auditLogger = new AuditLogger(new[] { sink }, loggerFactory.CreateLogger<AuditLogger>());
        var middleware = new AuditMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
            auditLogger,
            loggerFactory.CreateLogger<AuditMiddleware>());

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = Guid.NewGuid().ToString("N");
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/customers";
        httpContext.Request.RouteValues["id"] = Guid.NewGuid();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        sink.Events.Should().Contain(e => e.Action == "Customer.View" && e.Result == AuditResult.Denied);
    }
}
