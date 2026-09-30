using PayFlow.MockPaymentProvider.Capture;

namespace PayFlow.MockPaymentProvider.Refund;

public static class RefundPaymentEndpoints
{
    public static async Task<IResult> RefundAsync(
        HttpRequest httpRequest,
        RefundPaymentRequest request,
        MockRefundProviderState state,
        MockPaymentProviderOptions options,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.Headers.TryGetValue(
                "Idempotency-Key",
                out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(
                values[0]))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.");
        }

        try
        {
            var decision =
                state.Refund(
                    values[0]!,
                    request);

            switch (decision.Outcome)
            {
                case MockRefundOutcome.Succeeded:
                    return Success(decision);

                case MockRefundOutcome.Rejected:
                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status422UnprocessableEntity,
                        title:
                            "Mock refund was rejected.");

                case MockRefundOutcome.TimeoutBeforeProcessing:
                    await Task.Delay(
                            options.TimeoutSimulationDelay,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status504GatewayTimeout,
                        title:
                            "Mock refund timeout before processing.");

                case MockRefundOutcome.TimeoutAfterProcessing:
                    await Task.Delay(
                            options.TimeoutSimulationDelay,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return Success(decision);

                case MockRefundOutcome.ServerError:
                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status500InternalServerError,
                        title:
                            "Mock transient refund provider failure.");

                default:
                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status500InternalServerError,
                        title:
                            "Unsupported mock refund outcome.");
            }
        }
        catch (IdempotencyKeyConflictException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency key conflict.");
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid refund request.",
                detail: exception.Message);
        }
    }

    public static IResult ConfigureScenario(
        Guid refundId,
        ConfigureRefundScenarioRequest request,
        MockRefundProviderState state)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!MockRefundScenarioParser.TryParse(
                request.Scenario,
                out var scenario))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unsupported refund scenario.");
        }

        state.ConfigureScenario(
            refundId,
            scenario);

        return Results.NoContent();
    }

    public static IResult ResetScenario(
        Guid refundId,
        MockRefundProviderState state)
    {
        state.ResetScenario(
            refundId);

        return Results.NoContent();
    }

    private static IResult Success(
        MockRefundDecision decision)
    {
        return Results.Ok(
            new RefundPaymentResponse(
                decision.ProviderReference
                ?? throw new InvalidOperationException(
                    "Successful refund must contain provider reference.")));
    }
}
