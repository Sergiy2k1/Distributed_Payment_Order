using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Domain.Reservations;

namespace PayFlow.Inventory.Application.Reservations;

public sealed class ConsumeInventoryMessageHandler
    : IConsumeInventoryMessageHandler
{
    private readonly IInventoryReservationRepository _reservationRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IInventoryOutboxWriter _outboxWriter;
    private readonly IInventoryUnitOfWork _unitOfWork;

    public ConsumeInventoryMessageHandler(
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
        ConsumeInventoryMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Payload.OrderId == Guid.Empty
            || message.Payload.ReservationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrderId and ReservationId must be non-empty.",
                nameof(message));
        }

        if (message.Envelope.AggregateId
            != message.Payload.OrderId)
        {
            throw new ArgumentException(
                "ConsumeInventory payload OrderId must match envelope AggregateId.",
                nameof(message));
        }

        var reservation =
            await _reservationRepository.GetByIdAsync(
                message.Payload.ReservationId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Inventory reservation '{message.Payload.ReservationId:D}' does not exist.");

        if (reservation.OrderId != message.Payload.OrderId)
        {
            throw new InvalidOperationException(
                "ConsumeInventory OrderId does not match persisted reservation.");
        }

        if (reservation.Status == InventoryReservationStatus.Consumed)
        {
            return;
        }

        if (reservation.Status != InventoryReservationStatus.Reserved)
        {
            throw new InvalidOperationException(
                $"Inventory reservation cannot be consumed from state '{reservation.Status}'.");
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
                "Reserved stock row is missing for one or more reservation items.");
        }

        foreach (var item in reservation.Items)
        {
            stockBySku[item.SkuId]
                .Consume(item.Quantity);
        }

        reservation.Consume(
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
            "InventoryConsumed.v1",
            new InventoryConsumedV1(
                reservation.OrderId,
                reservation.ReservationId,
                message.Envelope.OccurredAtUtc),
            message.Envelope.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
