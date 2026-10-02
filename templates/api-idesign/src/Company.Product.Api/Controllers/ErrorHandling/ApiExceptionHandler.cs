using Company.Product.Api.Accessors.Exceptions;
using Company.Product.Api.Managers.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Company.Product.Api.Controllers.ErrorHandling;

/// <summary>
/// Turns layer exceptions into ProblemDetails responses so Controllers never need try/catch.
/// Unknown exceptions fall through to the default 500 handler.
/// </summary>
/// <param name="problemDetailsService">Writes ProblemDetails responses.</param>
/// <param name="logger">Logger.</param>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            BusinessValidationException validation => new ValidationProblemDetails(
                validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = validation.Message,
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = notFound.Message,
            },
            ResourceAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "A backing resource is unavailable.",
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        if (exception is ResourceAccessException)
        {
            logger.LogError(exception, "Resource access failed");
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
