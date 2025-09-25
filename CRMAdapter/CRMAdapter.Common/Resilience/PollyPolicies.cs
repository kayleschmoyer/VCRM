// File: PollyPolicies.cs
// Summary: Centralized Polly policy definitions for HTTP and infrastructure resilience.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Polly.Wrap;

namespace CRMAdapter.Common.Resilience;

/// <summary>
/// Provides factory methods for resilience policies shared across the solution.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Creates a composite HTTP resilience pipeline consisting of retry, circuit breaker, and timeout guards.
    /// </summary>
    /// <param name="options">Optional overrides for retry/backoff behavior.</param>
    /// <returns>An asynchronous policy wrap suitable for HttpClient usage.</returns>
    public static AsyncPolicyWrap<HttpResponseMessage> CreateHttpPolicy(PollyPolicyOptions? options = null)
    {
        options ??= PollyPolicyOptions.Default;
        var retryPolicy = BuildHttpRetryPolicy(options);
        var circuitBreaker = BuildHttpCircuitBreaker(options);
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(NormalizeTimeout(options.Timeout));
        return Policy.WrapAsync(timeoutPolicy, circuitBreaker, retryPolicy);
    }

    /// <summary>
    /// Creates a non-generic resilience pipeline for tasks interacting with downstream dependencies (SQL, message buses, etc.).
    /// </summary>
    /// <param name="options">Optional overrides for retry/backoff behavior.</param>
    /// <returns>An asynchronous policy wrap.</returns>
    public static AsyncPolicyWrap CreateNonResultPolicy(PollyPolicyOptions? options = null)
    {
        options ??= PollyPolicyOptions.Default;
        var retryPolicy = Policy
            .Handle<DbException>()
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutRejectedException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(options.RetryCount, attempt => ComputeBackoff(options, attempt));

        var circuitBreaker = Policy
            .Handle<DbException>()
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutRejectedException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(options.CircuitBreakerAllowedFailures, NormalizeBreakDuration(options.CircuitBreakerDuration));

        var timeoutPolicy = Policy.TimeoutAsync(NormalizeTimeout(options.Timeout));

        return Policy.WrapAsync(timeoutPolicy, circuitBreaker, retryPolicy);
    }

    private static AsyncPolicy<HttpResponseMessage> BuildHttpRetryPolicy(PollyPolicyOptions options)
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutRejectedException>()
            .Or<TimeoutException>()
            .OrResult(response => response is not null && IsTransientStatusCode(response.StatusCode, options))
            .WaitAndRetryAsync(options.RetryCount, attempt => ComputeBackoff(options, attempt));
    }

    private static AsyncPolicy<HttpResponseMessage> BuildHttpCircuitBreaker(PollyPolicyOptions options)
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutRejectedException>()
            .Or<TimeoutException>()
            .OrResult(response => response is not null && IsTransientStatusCode(response.StatusCode, options))
            .CircuitBreakerAsync(options.CircuitBreakerAllowedFailures, NormalizeBreakDuration(options.CircuitBreakerDuration));
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode, PollyPolicyOptions options)
    {
        var numeric = (int)statusCode;
        if (numeric >= 500)
        {
            return true;
        }

        if (statusCode == HttpStatusCode.RequestTimeout || numeric == 429)
        {
            return true;
        }

        return options.AdditionalTransientHttpStatusCodes.Contains(statusCode);
    }

    private static TimeSpan ComputeBackoff(PollyPolicyOptions options, int attempt)
    {
        var baseDelay = options.BaseDelay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(200) : options.BaseDelay;
        var exponent = Math.Pow(2, Math.Max(0, attempt - 1));
        var milliseconds = Math.Min(baseDelay.TotalMilliseconds * exponent, options.MaxBackoff.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static TimeSpan NormalizeTimeout(TimeSpan timeout)
    {
        return timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : timeout;
    }

    private static TimeSpan NormalizeBreakDuration(TimeSpan duration)
    {
        return duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : duration;
    }
}

/// <summary>
/// Tunable options for the shared Polly policies.
/// </summary>
public sealed class PollyPolicyOptions
{
    private static readonly Lazy<PollyPolicyOptions> LazyDefault = new(() => new PollyPolicyOptions());

    /// <summary>
    /// Gets the singleton default options instance.
    /// </summary>
    public static PollyPolicyOptions Default => LazyDefault.Value;

    /// <summary>
    /// Gets or sets the number of retry attempts to perform on transient faults.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay used when calculating exponential backoff.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum amount of time allowed before the circuit resets.
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of faults allowed before the circuit breaker opens.
    /// </summary>
    public int CircuitBreakerAllowedFailures { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout applied to guarded operations.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum backoff duration used during retries.
    /// </summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the set of additional HTTP status codes considered transient.
    /// </summary>
    public ISet<HttpStatusCode> AdditionalTransientHttpStatusCodes { get; } = new HashSet<HttpStatusCode>();
}
