using PayFlow.Order.Application.Orders.CreateOrder;

namespace PayFlow.Order.Api.Endpoints.Orders.CreateOrder;

public static class CreateOrderEndpoint
{
    public static IEndpointRouteBuilder MapCreateOrderEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/orders",
                async (
                    HttpContext httpContext,
                    CreateOrderRequest request,
                    CreateOrderHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors =
                        CreateOrderRequestValidator.Validate(request);

                    var idempotencyKeyValues =
                        httpContext.Request.Headers["Idempotency-Key"];

                    if (idempotencyKeyValues.Count > 1)
                    {
                        validationErrors["Idempotency-Key"] =
                        [
                            "Idempotency-Key must contain a single value."
                        ];
                    }

                    var idempotencyKey =
                        idempotencyKeyValues.Count == 0
                            ? null
                            : idempotencyKeyValues.ToString();

                    if (idempotencyKey is not null
                        && (string.IsNullOrWhiteSpace(idempotencyKey)
                            || idempotencyKey.Length > 128))
                    {
                        validationErrors["Idempotency-Key"] =
                        [
                            "Idempotency-Key must be non-empty and cannot exceed 128 characters."
                        ];
                    }

                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(
                            validationErrors);
                    }

                    var command = new CreateOrderCommand(
                        request.CustomerId,
                        request.Items
                            .Select(static item =>
                                new CreateOrderItem(
                                    item.Sku,
                                    item.Quantity,
                                    item.UnitPrice,
                                    item.Currency))
                            .ToArray());

                    var result = await handler
                        .HandleAsync(
                            command,
                            idempotencyKey,
                            cancellationToken)
                        .ConfigureAwait(false);

                    var response = new CreateOrderResponse(
                        result.OrderId,
                        result.Status.ToString(),
                        result.TotalAmount,
                        result.Currency);

                    return Results.Created(
                        $"/orders/{result.OrderId}",
                        response);
                })
            .WithName("CreateOrder")
            .Produces<CreateOrderResponse>(
                StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(
                StatusCodes.Status409Conflict);

        return endpoints;
    }
}
