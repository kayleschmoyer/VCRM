// ChangeDispatcher.cs: Default implementation that replays queued changes against the CRM API.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Core.Sync;

public sealed class ChangeDispatcher : IChangeDispatcher
{
    private static readonly IReadOnlyDictionary<string, string> EntityRoutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Customers"] = "customers",
        ["Vehicles"] = "vehicles",
        ["Invoices"] = "invoices",
        ["Appointments"] = "appointments",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChangeDispatcher> _logger;
    private readonly OfflineSyncState _state;

    public ChangeDispatcher(IHttpClientFactory httpClientFactory, ILogger<ChangeDispatcher> logger, OfflineSyncState state)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public async Task<ChangeDispatchResult> DispatchAsync(ChangeEnvelope change, CancellationToken cancellationToken)
    {
        if (change is null)
        {
            throw new ArgumentNullException(nameof(change));
        }

        if (!EntityRoutes.TryGetValue(change.EntityType, out var route))
        {
            _logger.LogWarning("No route mapping exists for entity type {EntityType}. Change {ChangeId} will be dropped.", change.EntityType, change.CorrelationId);
            return ChangeDispatchResult.Failed("Unknown entity type.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientNames.CrmApi);
            using var request = BuildRequest(change, route);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var serverPayload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var serverTimestamp = TryParseTimestamp(response.Headers);
                _logger.LogWarning("Conflict detected while replaying change {ChangeId} for {EntityType} {EntityId}.", change.CorrelationId, change.EntityType, change.EntityId);
                return ChangeDispatchResult.ConflictDetected(serverTimestamp, serverPayload, "Conflict detected by API.");
            }

            response.EnsureSuccessStatusCode();

            var timestamp = TryParseTimestamp(response.Headers);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Successfully replayed change {ChangeId} for {EntityType} {EntityId}.", change.CorrelationId, change.EntityType, change.EntityId);
            _state.SetOffline(false);
            return ChangeDispatchResult.Successful(timestamp, string.IsNullOrWhiteSpace(payload) ? null : payload);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch change {ChangeId} for {EntityType} {EntityId}.", change.CorrelationId, change.EntityType, change.EntityId);
            _state.SetOffline(true);
            return ChangeDispatchResult.Failed(ex.Message);
        }
    }

    private static HttpRequestMessage BuildRequest(ChangeEnvelope change, string route)
    {
        var uri = route.TrimEnd('/');
        if (change.Operation != ChangeOperation.Create)
        {
            uri += "/" + change.EntityId;
        }

        HttpMethod method = change.Operation switch
        {
            ChangeOperation.Create => HttpMethod.Post,
            ChangeOperation.Update => HttpMethod.Put,
            ChangeOperation.Delete => HttpMethod.Delete,
            _ => HttpMethod.Post,
        };

        var request = new HttpRequestMessage(method, uri);
        if (change.Operation != ChangeOperation.Delete && !string.IsNullOrWhiteSpace(change.Payload))
        {
            request.Content = new StringContent(change.Payload!, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static DateTimeOffset? TryParseTimestamp(HttpResponseHeaders headers)
    {
        if (headers is null)
        {
            return null;
        }

        if (headers.TryGetValues("X-Server-Timestamp", out var values))
        {
            foreach (var value in values)
            {
                if (DateTimeOffset.TryParse(value, out var timestamp))
                {
                    return timestamp;
                }
            }
        }

        return null;
    }
}
