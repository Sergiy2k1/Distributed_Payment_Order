namespace PayFlow.MockPaymentProvider.Webhooks;

public static class MockWebhookScenarioEndpoints
{
    public static IResult ConfigureCapture(
        Guid paymentId,
        ConfigureWebhookScenarioRequest request,
        MockWebhookScenarioState state)
    {
        if (!MockWebhookScenarioParser.TryParse(request.Scenario, out var scenario))
        {
            return Results.BadRequest();
        }

        state.ConfigureCapture(paymentId, scenario);
        return Results.NoContent();
    }

    public static IResult ResetCapture(
        Guid paymentId,
        MockWebhookScenarioState state)
    {
        state.ResetCapture(paymentId);
        return Results.NoContent();
    }

    public static IResult ConfigureRefund(
        Guid refundId,
        ConfigureWebhookScenarioRequest request,
        MockWebhookScenarioState state)
    {
        if (!MockWebhookScenarioParser.TryParse(request.Scenario, out var scenario))
        {
            return Results.BadRequest();
        }

        state.ConfigureRefund(refundId, scenario);
        return Results.NoContent();
    }

    public static IResult ResetRefund(
        Guid refundId,
        MockWebhookScenarioState state)
    {
        state.ResetRefund(refundId);
        return Results.NoContent();
    }
}
