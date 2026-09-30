namespace PayFlow.MockPaymentProvider.Capture;

public static class CapturePaymentEndpoints
{
    public static async Task<IResult> CaptureAsync(
        HttpRequest httpRequest,
        CapturePaymentRequest request,
        MockPaymentProviderState state,
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
                state.Capture(
                    values[0]!,
                    request);

            switch (decision.Outcome)
            {
                case MockCaptureOutcome.Succeeded:
                    return Success(decision);

                case MockCaptureOutcome.Declined:
                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status402PaymentRequired,
                        title:
                            "Mock payment was declined.");

                case MockCaptureOutcome.TimeoutBeforeProcessing:
                    await Task.Delay(
                            options.TimeoutSimulationDelay,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status504GatewayTimeout,
                        title:
                            "Mock timeout before processing.");

                case MockCaptureOutcome.TimeoutAfterProcessing:
                    await Task.Delay(
                            options.TimeoutSimulationDelay,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return Success(decision);

                case MockCaptureOutcome.ServerError:
                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status500InternalServerError,
                        title:
                            "Mock transient provider failure.");

                default:
                    return Results.Problem(
                        statusCode:
                            StatusCodes.Status500InternalServerError,
                        title:
                            "Unsupported mock capture outcome.");
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
                title: "Invalid capture request.",
                detail: exception.Message);
        }
    }

    public static IResult ConfigureScenario(
        Guid paymentId,
        ConfigurePaymentScenarioRequest request,
        MockPaymentProviderState state)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!MockPaymentScenarioParser.TryParse(
                request.Scenario,
                out var scenario))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unsupported payment scenario.");
        }

        state.ConfigureScenario(
            paymentId,
            scenario);

        return Results.NoContent();
    }

    public static IResult ResetScenario(
        Guid paymentId,
        MockPaymentProviderState state)
    {
        state.ResetScenario(
            paymentId);

        return Results.NoContent();
    }

    private static IResult Success(
        MockCaptureDecision decision)
    {
        return Results.Ok(
            new CapturePaymentResponse(
                decision.ProviderReference
                ?? throw new InvalidOperationException(
                    "Successful capture must contain provider reference.")));
    }
}
