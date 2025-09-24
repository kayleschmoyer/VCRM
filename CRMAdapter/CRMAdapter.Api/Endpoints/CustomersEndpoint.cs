// File: CustomersEndpoint.cs
// Summary: Declares customer-specific endpoint mappings and request contracts.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
/// Maps canonical customer operations to minimal API endpoints.
/// </summary>
public static class CustomersEndpoint
{
    private const int DefaultMaxResults = 50;
    private const int MaxAllowedResults = 500;

    /// <summary>
    /// Registers customer endpoints on the supplied route builder.
    /// </summary>
    /// <param name="endpoints">Endpoint builder used to map HTTP routes.</param>
    /// <returns>The configured route group builder.</returns>
    public static RouteGroupBuilder MapCustomersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var group = endpoints.MapGroup("/customers").WithTags("Customers").WithOpenApi();

        group.MapGet("/{id:guid}", GetCustomerByIdAsync)
            .WithName("GetCustomerById")
            .WithSummary("Retrieves a canonical customer by identifier.")
            .WithDescription("Returns the canonical customer aggregate projected by the configured adapter.")
            .RequireAuthorization(AuthPolicies.CustomerRead)
            .Produces<Customer>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/search", SearchCustomersAsync)
            .WithName("SearchCustomers")
            .WithSummary("Performs a canonical customer search.")
            .WithDescription("Invokes the configured adapter search pipeline using canonical semantics.")
            .RequireAuthorization(AuthPolicies.CustomerSearch)
            .Produces<IReadOnlyCollection<Customer>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Ok<Customer>, ProblemHttpResult>> GetCustomerByIdAsync(
        Guid id,
        HttpContext httpContext,
        ICustomerAdapter adapter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (adapter is null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        var logger = loggerFactory.CreateLogger(typeof(CustomersEndpoint));
        logger.LogInformation("Resolving customer {CustomerId}.", id);

        var customer = await adapter.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (customer is null)
        {
            logger.LogWarning("Customer {CustomerId} was not found.", id);
            return TypedResults.Problem(CreateNotFoundProblem(httpContext, $"Customer '{id}' was not located."));
        }

        return TypedResults.Ok(customer);
    }

    private static async Task<Results<Ok<IReadOnlyCollection<Customer>>, ProblemHttpResult>> SearchCustomersAsync(
        CustomerSearchRequest request,
        ICustomerAdapter adapter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (adapter is null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        if (request is null)
        {
            return TypedResults.Problem(new ProblemDetails
            {
                Title = "Invalid request payload.",
                Detail = "Request body cannot be empty.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return TypedResults.Problem(new ProblemDetails
            {
                Title = "Invalid search criteria.",
                Detail = "A search query must be supplied.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var normalizedMax = Math.Clamp(request.MaxResults ?? DefaultMaxResults, 1, MaxAllowedResults);
        var normalizedQuery = request.Query.Trim();
        var logger = loggerFactory.CreateLogger(typeof(CustomersEndpoint));
        logger.LogInformation("Executing customer search for '{Query}' with limit {Limit}.", normalizedQuery, normalizedMax);

        var results = await adapter.SearchByNameAsync(normalizedQuery, normalizedMax, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(results);
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

/// <summary>
/// Request payload used to trigger canonical customer search queries.
/// </summary>
/// <param name="Query">Name fragment or identifier to search for.</param>
/// <param name="MaxResults">Optional limit on the number of results (defaults to 50).</param>
public sealed record CustomerSearchRequest(
    [property: Required] string Query,
    [property: Range(1, MaxAllowedResults)] int? MaxResults)
{
    private const int MaxAllowedResults = 500;
}
