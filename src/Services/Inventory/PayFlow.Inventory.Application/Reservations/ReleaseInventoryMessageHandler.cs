using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Domain.Reservations;

namespace PayFlow.Inventory.Application.Reservations;

public sealed class ReleaseInventoryMessageHandler
    : IReleaseInventoryMessageHandler
{
    private readonly IInventoryReservationRepository _reservationRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IInventoryOutboxWriter _outboxWriter;
    private readonly IInventoryUnitOfWork _unitOfWork;

    public ReleaseInventoryMessageHandler(
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
        ReleaseInventoryMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Payload.ReasonCode);

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
                "ReleaseInventory payload OrderId must match envelope AggregateId.",
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
                "ReleaseInventory OrderId does not match persisted reservation.");
        }

        if (reservation.Status == InventoryReservationStatus.Released)
        {
            return;
        }

        if (reservation.Status != InventoryReservationStatus.Reserved)
        {
            throw new InvalidOperationException(
                $"Inventory reservation cannot be released from state '{reservation.Status}'.");
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
                .Release(item.Quantity);
        }

        reservation.Release(
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
            "InventoryReleased.v1",
            new InventoryReleasedV1(
                reservation.OrderId,
                reservation.ReservationId,
                message.Envelope.OccurredAtUtc,
                message.Payload.ReasonCode),
            message.Envelope.TraceParent,
            cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }
}
