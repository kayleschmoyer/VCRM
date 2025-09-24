// File: ExceptionMiddleware.cs
// Summary: Converts unhandled exceptions into RFC 7807 ProblemDetails responses with sanitized payloads.
using System;
using System.Net;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.Api.Middleware;

/// <summary>
/// Captures unhandled exceptions and renders standardized error responses.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next component in the pipeline.</param>
    /// <param name="logger">Logger used to capture diagnostics.</param>
    /// <param name="environment">Host environment to control error detail exposure.</param>
    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (CustomerNotFoundException notFound)
        {
            _logger.LogWarning(notFound, "Customer not found: {Message}", notFound.Message);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, notFound.Message, notFound.ErrorCode).ConfigureAwait(false);
        }
        catch (InvalidAdapterRequestException invalidRequest)
        {
            _logger.LogInformation(invalidRequest, "Adapter validation failure: {Message}", invalidRequest.Message);
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, invalidRequest.Message, invalidRequest.ErrorCode).ConfigureAwait(false);
        }
        catch (AdapterDataAccessException dataAccess)
        {
            _logger.LogError(dataAccess, "Adapter data access failure while executing {Operation}.", dataAccess.Operation);
            await WriteProblemAsync(context, StatusCodes.Status503ServiceUnavailable, "The CRM backend is currently unavailable. Please retry.", dataAccess.ErrorCode).ConfigureAwait(false);
        }
        catch (AdapterException adapterException)
        {
            _logger.LogError(adapterException, "Unhandled adapter exception: {ErrorCode}", adapterException.ErrorCode);
            await WriteProblemAsync(context, StatusCodes.Status502BadGateway, "The CRM adapter encountered an unexpected condition.", adapterException.ErrorCode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing request.");
            var detail = _environment.IsDevelopment() ? ex.Message : "An unexpected server error occurred.";
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, detail, "API-UNHANDLED").ConfigureAwait(false);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, int statusCode, string detail, string errorCode)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started; skipping problem response for status {StatusCode}.", statusCode);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var correlationId = GetCorrelationId(context);
        var problem = new ProblemDetails
        {
            Title = ReasonPhrases.GetReasonPhrase(statusCode) ?? "Unexpected error",
            Detail = detail,
            Status = statusCode,
            Instance = context.Request.Path,
            Type = "https://datatracker.ietf.org/doc/html/rfc7807",
        };

        problem.Extensions["errorCode"] = errorCode;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
    }

    private static string? GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationHeaderName, out var value)
            && value is string correlationId)
        {
            return correlationId;
        }

        return context.TraceIdentifier;
    }
}
