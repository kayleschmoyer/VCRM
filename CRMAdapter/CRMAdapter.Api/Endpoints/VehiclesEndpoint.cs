// File: VehiclesEndpoint.cs
// Summary: Declares vehicle-specific endpoint mappings over the canonical adapter abstraction.
using System;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonDomain;
using CRMAdapter.CommonSecurity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.Api.Endpoints;

/// <summary>
/// Maps canonical vehicle retrieval operations.
/// </summary>
public static class VehiclesEndpoint
{
    /// <summary>
    /// Registers vehicle endpoints on the supplied route builder.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <returns>The configured route group builder.</returns>
    public static RouteGroupBuilder MapVehiclesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var group = endpoints.MapGroup("/vehicles").WithTags("Vehicles").WithOpenApi();

        group.MapGet("/{id:guid}", GetVehicleByIdAsync)
            .WithName("GetVehicleById")
            .WithSummary("Retrieves a canonical vehicle by identifier.")
            .WithDescription("Returns the canonical vehicle aggregate exposed by the adapter layer.")
            .RequireAuthorization(RbacPolicy.GetPolicyName(RbacAction.VehicleView))
            .Produces<Vehicle>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Ok<Vehicle>, ProblemHttpResult>> GetVehicleByIdAsync(
        Guid id,
        HttpContext httpContext,
        IVehicleAdapter adapter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (adapter is null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        var logger = loggerFactory.CreateLogger(typeof(VehiclesEndpoint));
        logger.LogInformation("Resolving vehicle {VehicleId}.", id);

        var vehicle = await adapter.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            logger.LogWarning("Vehicle {VehicleId} was not found.", id);
            return TypedResults.Problem(CreateNotFoundProblem(httpContext, $"Vehicle '{id}' was not located."));
        }

        return TypedResults.Ok(vehicle);
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
