// BaseApiClient.cs: Shared plumbing for HTTP calls, JWT attachment, and future resiliency policies.
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.UI.Services.Api;

public abstract class BaseApiClient
{
    protected BaseApiClient(HttpClient client)
    {
        Client = client;
    }

    protected HttpClient Client { get; }

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

    protected virtual Task ApplyResiliencyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // TODO: Wire Polly or HttpClientFactory policies once the live API surface is ready.
        return Task.CompletedTask;
    }
}
