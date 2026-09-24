using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Order.Application.Orders.CreateOrder;

namespace PayFlow.Order.Api.Errors;

public sealed class IdempotencyConflictExceptionHandler
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not CreateOrderIdempotencyConflictException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Idempotency conflict.",
            Detail = exception.Message
        };

        problemDetails.Extensions["traceId"] =
            httpContext.TraceIdentifier;

        httpContext.Response.StatusCode =
            StatusCodes.Status409Conflict;

        await httpContext.Response
            .WriteAsJsonAsync(
                problemDetails,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
