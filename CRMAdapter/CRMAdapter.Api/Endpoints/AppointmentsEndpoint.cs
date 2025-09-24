// File: AppointmentsEndpoint.cs
// Summary: Declares appointment-specific endpoint mappings over the canonical adapter abstraction.
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
/// Maps canonical appointment retrieval endpoints.
/// </summary>
public static class AppointmentsEndpoint
{
    /// <summary>
    /// Registers appointment endpoints on the supplied route builder.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <returns>The configured route group builder.</returns>
    public static RouteGroupBuilder MapAppointmentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        var group = endpoints.MapGroup("/appointments").WithTags("Appointments").WithOpenApi();

        group.MapGet("/{id:guid}", GetAppointmentByIdAsync)
            .WithName("GetAppointmentById")
            .WithSummary("Retrieves a canonical appointment by identifier.")
            .WithDescription("Returns the canonical appointment representation produced by the adapter layer.")
            .RequireAuthorization(RbacPolicy.GetPolicyName(RbacAction.AppointmentView))
            .Produces<Appointment>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Ok<Appointment>, ProblemHttpResult>> GetAppointmentByIdAsync(
        Guid id,
        HttpContext httpContext,
        IAppointmentAdapter adapter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (adapter is null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        var logger = loggerFactory.CreateLogger(typeof(AppointmentsEndpoint));
        logger.LogInformation("Resolving appointment {AppointmentId}.", id);

        var appointment = await adapter.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (appointment is null)
        {
            logger.LogWarning("Appointment {AppointmentId} was not found.", id);
            return TypedResults.Problem(CreateNotFoundProblem(httpContext, $"Appointment '{id}' was not located."));
        }

        return TypedResults.Ok(appointment);
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
