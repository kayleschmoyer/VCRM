// RateLimitTests.cs: Verifies rate limiting middleware and counters enforce configured policies.
using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using CRMAdapter.Api.Configuration;
using CRMAdapter.Api.Middleware;
using CRMAdapter.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMAdapter.Tests.ResilienceTests;

public sealed class RateLimitTests
{
    [Fact]
    public void Evaluate_ShouldThrottleAuthenticatedUserAfterLimit()
    {
        // Arrange
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var service = new RequestCounterService(timeProvider);
        var settings = new RateLimitSettings();
        var context = new RateLimitRequestContext
        {
            Method = HttpMethods.Get,
            OriginalPath = "/customers",
            NormalizedPath = "/customers",
            IsAuthenticated = true,
            UserId = "user-1",
        };

        // Act
        RateLimitDecision decision = RateLimitDecision.Allowed;
        for (var i = 0; i < settings.AuthenticatedUser.RequestsPerMinute; i++)
        {
            decision = service.Evaluate(context, settings);
            Assert.True(decision.IsAllowed, "Expected request {0} to be allowed.", i + 1);
        }

        var blocked = service.Evaluate(context, settings);

        // Assert
        Assert.False(blocked.IsAllowed);
        Assert.Equal(RateLimitScopes.User, blocked.Scope);
    }

    [Fact]
    public async Task Middleware_ShouldIsolateCountersPerUser()
    {
        // Arrange
        var settings = new RateLimitSettings();
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var counterService = new RequestCounterService(timeProvider);
        var options = new TestOptionsMonitor<RateLimitSettings>(settings);
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            counterService,
            options,
            NullLogger<RateLimitMiddleware>.Instance);

        // Saturate user A.
        for (var i = 0; i < settings.AuthenticatedUser.RequestsPerMinute; i++)
        {
            var httpContext = CreateContext("/vehicles", isAuthenticated: true, userId: "alice");
            await middleware.InvokeAsync(httpContext);
            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        }

        // User B should still be allowed.
        var userBContext = CreateContext("/vehicles", isAuthenticated: true, userId: "bob");
        await middleware.InvokeAsync(userBContext);

        Assert.Equal(StatusCodes.Status200OK, userBContext.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_ShouldHonorCriticalEndpointOverrides()
    {
        // Arrange
        var settings = new RateLimitSettings();
        settings.EndpointOverrides["/health"] = new EndpointRateLimit
        {
            UnauthenticatedRequestsPerMinute = 300,
        };

        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var counterService = new RequestCounterService(timeProvider);
        var options = new TestOptionsMonitor<RateLimitSettings>(settings);
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            counterService,
            options,
            NullLogger<RateLimitMiddleware>.Instance);

        // Act & Assert: ensure thirty health checks succeed under the elevated cap.
        for (var i = 0; i < 30; i++)
        {
            var httpContext = CreateContext("/health", isAuthenticated: false, userId: null, ipAddress: "10.0.0.1");
            await middleware.InvokeAsync(httpContext);
            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        }
    }

    private static DefaultHttpContext CreateContext(string path, bool isAuthenticated, string? userId, string? ipAddress = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (isAuthenticated && !string.IsNullOrWhiteSpace(userId))
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test");
            context.User = new ClaimsPrincipal(identity);
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        }

        return context;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; private set; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
