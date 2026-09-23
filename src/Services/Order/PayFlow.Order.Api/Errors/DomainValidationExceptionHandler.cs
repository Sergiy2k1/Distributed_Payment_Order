using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PayFlow.Order.Api.Errors;

public sealed class DomainValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ArgumentException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request is invalid.",
            Detail = exception.Message
        };

        problemDetails.Extensions["traceId"] =
            httpContext.TraceIdentifier;

        httpContext.Response.StatusCode =
            StatusCodes.Status400BadRequest;

        await httpContext.Response
            .WriteAsJsonAsync(
                problemDetails,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
