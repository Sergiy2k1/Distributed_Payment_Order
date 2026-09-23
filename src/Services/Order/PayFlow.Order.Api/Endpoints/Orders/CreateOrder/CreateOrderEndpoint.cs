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
                    CreateOrderRequest request,
                    CreateOrderHandler handler,
                    CancellationToken cancellationToken) =>
                {
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
                        .HandleAsync(command, cancellationToken)
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
            .WithName("CreateOrder");

        return endpoints;
    }
}
