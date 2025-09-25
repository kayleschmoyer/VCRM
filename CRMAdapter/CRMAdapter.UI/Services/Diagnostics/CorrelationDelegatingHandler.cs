// CorrelationDelegatingHandler.cs: Ensures correlation identifiers flow between UI HTTP calls and API responses.
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CRMAdapter.UI.Services.Diagnostics;

/// <summary>
/// Adds correlation headers to outbound API requests and updates the local context from responses.
/// </summary>
public sealed class CorrelationDelegatingHandler : DelegatingHandler
{
    private readonly CorrelationContext _correlationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationDelegatingHandler"/> class.
    /// </summary>
    /// <param name="correlationContext">Context tracking the current UI correlation identifier.</param>
    public CorrelationDelegatingHandler(CorrelationContext correlationContext)
    {
        _correlationContext = correlationContext ?? throw new ArgumentNullException(nameof(correlationContext));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(CorrelationContext.HeaderName);
        request.Headers.TryAddWithoutValidation(CorrelationContext.HeaderName, _correlationContext.CurrentCorrelationId);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Headers.TryGetValues(CorrelationContext.HeaderName, out var values))
        {
            var headerValue = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                _correlationContext.SetCorrelationId(headerValue);
            }
        }
        else if (request.Headers.TryGetValues(CorrelationContext.HeaderName, out var outbound))
        {
            var headerValue = outbound.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                _correlationContext.SetCorrelationId(headerValue);
            }
        }

        return response;
    }
}
