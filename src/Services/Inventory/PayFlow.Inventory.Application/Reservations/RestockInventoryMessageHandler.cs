using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Domain.Reservations;

namespace PayFlow.Inventory.Application.Reservations;

public sealed class RestockInventoryMessageHandler
    : IRestockInventoryMessageHandler
{
    private readonly IInventoryReservationRepository _reservationRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IInventoryOutboxWriter _outboxWriter;
    private readonly IInventoryUnitOfWork _unitOfWork;

    public RestockInventoryMessageHandler(
        IInventoryReservationRepository reservationRepository,
        IStockRepository stockRepository,
        IInventoryOutboxWriter outboxWriter,
        IInventoryUnitOfWork unitOfWork)
    {
        _reservationRepository = reservationRepository;
        _stockRepository = stockRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        RestockInventoryMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

        if (message.Payload.OrderId == Guid.Empty
            || message.Payload.ReservationId == Guid.Empty
            || message.Payload.RestockOperationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrderId, ReservationId and RestockOperationId must be non-empty.",
                nameof(message));
        }

        if (message.Envelope.AggregateId
            != message.Payload.OrderId)
        {
            throw new ArgumentException(
                "RestockInventory payload OrderId must match envelope AggregateId.",
                nameof(message));
        }

        var reservation =
            await _reservationRepository.GetByIdAsync(
                message.Payload.ReservationId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Inventory reservation '{message.Payload.ReservationId:D}' does not exist.");

        if (reservation.OrderId
            != message.Payload.OrderId)
        {
            throw new InvalidOperationException(
                "RestockInventory OrderId does not match persisted reservation.");
        }

        if (reservation.Status
            == InventoryReservationStatus.Restocked)
        {
            if (reservation.RestockOperationId
                == message.Payload.RestockOperationId)
            {
                return;
            }

            throw new InvalidOperationException(
                "Inventory reservation was already restocked by a different operation.");
        }

        if (reservation.Status
            != InventoryReservationStatus.Consumed)
        {
            throw new InvalidOperationException(
                $"Inventory reservation cannot be restocked from state '{reservation.Status}'.");
        }

        var lockedStock =
            await _stockRepository.LockBySkuIdsAsync(
                reservation.Items
                    .Select(static item => item.SkuId)
                    .ToArray(),
                cancellationToken)
            .ConfigureAwait(false);

        var stockBySku =
            lockedStock.ToDictionary(
                static item => item.SkuId,
                StringComparer.Ordinal);

        if (reservation.Items.Any(
                item => !stockBySku.ContainsKey(item.SkuId)))
        {
            throw new InvalidOperationException(
                "Consumed stock row is missing for one or more reservation items.");
        }

        foreach (var item in reservation.Items)
        {
            stockBySku[item.SkuId]
                .Restock(item.Quantity);
        }

        reservation.Restock(
            message.Payload.RestockOperationId,
            message.Envelope.OccurredAtUtc);

        await _stockRepository.ApplyAsync(
            lockedStock,
            cancellationToken)
            .ConfigureAwait(false);

        await _reservationRepository.ApplyAsync(
            reservation,
            cancellationToken)
            .ConfigureAwait(false);

        await _outboxWriter.AddAsync(
            reservation.OrderId,
            message.Envelope.CorrelationId,
            message.Envelope.MessageId,
            message.Envelope.OccurredAtUtc,
            "InventoryRestocked.v1",
            new InventoryRestockedV1(
                reservation.OrderId,
                reservation.ReservationId,
                message.Payload.RestockOperationId,
                message.Envelope.OccurredAtUtc),
            message.Envelope.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
