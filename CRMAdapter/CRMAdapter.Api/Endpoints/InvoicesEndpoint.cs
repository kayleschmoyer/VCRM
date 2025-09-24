// File: InvoicesEndpoint.cs
// Summary: Declares invoice-specific endpoint mappings over the canonical adapter abstraction.
using System;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.Api.Security;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonDomain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.Api.Endpoints;

/// <summary>
/// Maps canonical invoice retrieval endpoints.
/// </summary>
public static class InvoicesEndpoint
{
    /// <summary>
    /// Registers invoice endpoints on the supplied route builder.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <returns>The configured route group builder.</returns>
    public static RouteGroupBuilder MapInvoicesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var group = endpoints.MapGroup("/invoices").WithTags("Invoices").WithOpenApi();

        group.MapGet("/{id:guid}", GetInvoiceByIdAsync)
            .WithName("GetInvoiceById")
            .WithSummary("Retrieves a canonical invoice by identifier.")
            .WithDescription("Returns the canonical invoice projection produced by the adapter layer.")
            .RequireAuthorization(AuthPolicies.InvoiceRead)
            .Produces<Invoice>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Ok<Invoice>, ProblemHttpResult>> GetInvoiceByIdAsync(
        Guid id,
        HttpContext httpContext,
        IInvoiceAdapter adapter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (adapter is null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        var logger = loggerFactory.CreateLogger(typeof(InvoicesEndpoint));
        logger.LogInformation("Resolving invoice {InvoiceId}.", id);

        var invoice = await adapter.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            logger.LogWarning("Invoice {InvoiceId} was not found.", id);
            return TypedResults.Problem(CreateNotFoundProblem(httpContext, $"Invoice '{id}' was not located."));
        }

        return TypedResults.Ok(invoice);
    }

    private static ProblemDetails CreateNotFoundProblem(HttpContext context, string detail)
    {
        return new ProblemDetails
        {
            Title = "Resource not found.",
            Detail = detail,
            Status = StatusCodes.Status404NotFound,
            Instance = context?.Request.Path.ToString(),
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
        };
    }
}
