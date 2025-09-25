using System;
// BaseApiClient.cs: Shared plumbing for HTTP calls, JWT attachment, and future resiliency policies.
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.Common.Resilience;
using Polly;

namespace CRMAdapter.UI.Services.Api;

public abstract class BaseApiClient
{
    private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

    protected BaseApiClient(HttpClient client, IAsyncPolicy<HttpResponseMessage>? resiliencePolicy = null)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _resiliencePolicy = resiliencePolicy ?? PollyPolicies.CreateHttpPolicy();
    }

    protected HttpClient Client { get; }

    protected IAsyncPolicy<HttpResponseMessage> ResiliencePolicy => _resiliencePolicy;

    protected virtual ValueTask<string?> GetJwtAsync(CancellationToken cancellationToken)
    {
        // TODO: Integrate with AuthStateProvider or token cache when live API wiring is enabled.
        return ValueTask.FromResult<string?>(null);
    }

    protected async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string uri, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, uri);
        var token = await GetJwtAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    protected Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return _resiliencePolicy.ExecuteAsync(ct => Client.SendAsync(request, ct), cancellationToken);
    }

    protected virtual Task ApplyResiliencyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Maintained for backward compatibility. Use SendAsync for resilient HTTP calls.
        return Task.CompletedTask;
    }
}
