// TimeoutPolicyTests.cs: Ensures timeout guards trigger for long-running calls.
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.Common.Resilience;
using Polly.Timeout;
using Xunit;

namespace CRMAdapter.Tests.ResilienceTests;

public sealed class TimeoutPolicyTests
{
    [Fact]
    public async Task HttpPolicy_ShouldThrowTimeoutForSlowOperations()
    {
        // Arrange
        var options = new PollyPolicyOptions
        {
            RetryCount = 0,
            Timeout = TimeSpan.FromMilliseconds(100),
        };
        var policy = PollyPolicies.CreateHttpPolicy(options);

        async Task<HttpResponseMessage> SlowOperation(CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => policy.ExecuteAsync(SlowOperation, CancellationToken.None));
    }
}
