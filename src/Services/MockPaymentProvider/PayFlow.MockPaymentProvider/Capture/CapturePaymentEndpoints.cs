namespace PayFlow.MockPaymentProvider.Capture;

public static class CapturePaymentEndpoints
{
    public static IResult Capture(
        HttpRequest httpRequest,
        CapturePaymentRequest request,
        MockPaymentProviderState state)
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

            return decision.Scenario switch
            {
                MockPaymentScenario.Success =>
                    Results.Ok(
                        new CapturePaymentResponse(
                            decision.ProviderReference
                            ?? throw new InvalidOperationException(
                                "Successful capture must contain provider reference."))),

                MockPaymentScenario.Decline =>
                    Results.Problem(
                        statusCode:
                            StatusCodes.Status402PaymentRequired,
                        title:
                            "Mock payment was declined."),

                _ => Results.Problem(
                    statusCode:
                        StatusCodes.Status500InternalServerError,
                    title:
                        "Unsupported mock payment scenario.")
            };
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

        if (!Enum.TryParse<MockPaymentScenario>(
                request.Scenario,
                ignoreCase: true,
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
}
