// CircuitBreakerTests.cs: Validates circuit breaker opens after the configured number of faults.
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.Common.Resilience;
using Polly.CircuitBreaker;
using Xunit;

namespace CRMAdapter.Tests.ResilienceTests;

public sealed class CircuitBreakerTests
{
    [Fact]
    public async Task HttpPolicy_ShouldOpenCircuitAfterConsecutiveFailures()
    {
        // Arrange
        var options = new PollyPolicyOptions
        {
            RetryCount = 0,
            CircuitBreakerAllowedFailures = 2,
            CircuitBreakerDuration = TimeSpan.FromSeconds(30),
            Timeout = TimeSpan.FromSeconds(1),
        };
        var policy = PollyPolicies.CreateHttpPolicy(options);
        var attempts = 0;

        Task<HttpResponseMessage> ThrowAsync(CancellationToken token)
        {
            attempts++;
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("boom"));
        }

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => policy.ExecuteAsync(ThrowAsync, CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => policy.ExecuteAsync(ThrowAsync, CancellationToken.None));
        await Assert.ThrowsAsync<BrokenCircuitException>(() => policy.ExecuteAsync(ThrowAsync, CancellationToken.None));

        Assert.Equal(2, attempts);
    }
}
