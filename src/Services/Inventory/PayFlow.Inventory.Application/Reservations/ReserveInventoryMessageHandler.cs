using PayFlow.Inventory.Application.Abstractions;
using PayFlow.Inventory.Domain.Reservations;
using PayFlow.Inventory.Domain.Stock;

namespace PayFlow.Inventory.Application.Reservations;

public sealed class ReserveInventoryMessageHandler
    : IReserveInventoryMessageHandler
{
    public const string InsufficientStockReason =
        "INSUFFICIENT_STOCK";

    public const string StockNotFoundReason =
        "STOCK_NOT_FOUND";

    private readonly IInventoryReservationRepository _reservationRepository;
    private readonly IStockRepository _stockRepository;
    private readonly IInventoryOutboxWriter _outboxWriter;
    private readonly IInventoryUnitOfWork _unitOfWork;

    public ReserveInventoryMessageHandler(
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
        ReserveInventoryMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Envelope);
        ArgumentNullException.ThrowIfNull(message.Payload);
        ArgumentNullException.ThrowIfNull(message.Payload.Items);

        ValidateMessage(message);

        var existing = await _reservationRepository
            .GetByIdAsync(
                message.Payload.ReservationId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            EnsureEquivalent(existing, message.Payload);
            return;
        }

        var requestedItems = message.Payload.Items
            .Select(
                static item =>
                    InventoryReservationItem.Create(
                        item.SkuId,
                        item.Quantity))
            .ToArray();

        var reservation =
            InventoryReservation.Create(
                message.Payload.ReservationId,
                message.Payload.OrderId,
                requestedItems,
                message.Envelope.OccurredAtUtc,
                message.Payload.ExpiresAtUtc);

        var lockedStock = await _stockRepository
            .LockBySkuIdsAsync(
                requestedItems
                    .Select(static item => item.SkuId)
                    .ToArray(),
                cancellationToken)
            .ConfigureAwait(false);

        var stockBySku = lockedStock
            .ToDictionary(
                static stock => stock.SkuId,
                StringComparer.Ordinal);

        var rejectionReason =
            DetermineRejectionReason(
                requestedItems,
                stockBySku);

        if (rejectionReason is null)
        {
            foreach (var item in requestedItems)
            {
                stockBySku[item.SkuId]
                    .Reserve(item.Quantity);
            }

            reservation.MarkReserved(
                message.Envelope.OccurredAtUtc);

            await _stockRepository
                .ApplyAsync(
                    lockedStock,
                    cancellationToken)
                .ConfigureAwait(false);

            await _reservationRepository
                .AddAsync(
                    reservation,
                    cancellationToken)
                .ConfigureAwait(false);

            await _outboxWriter
                .AddAsync(
                    reservation.OrderId,
                    message.Envelope.CorrelationId,
                    message.Envelope.MessageId,
                    message.Envelope.OccurredAtUtc,
                    "InventoryReserved.v1",
                    new InventoryReservedV1(
                        reservation.OrderId,
                        reservation.ReservationId,
                        message.Envelope.OccurredAtUtc,
                        reservation.ExpiresAtUtc),
                    message.Envelope.TraceParent,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            reservation.Reject(
                rejectionReason,
                message.Envelope.OccurredAtUtc);

            await _reservationRepository
                .AddAsync(
                    reservation,
                    cancellationToken)
                .ConfigureAwait(false);

            await _outboxWriter
                .AddAsync(
                    reservation.OrderId,
                    message.Envelope.CorrelationId,
                    message.Envelope.MessageId,
                    message.Envelope.OccurredAtUtc,
                    "InventoryReservationRejected.v1",
                    new InventoryReservationRejectedV1(
                        reservation.OrderId,
                        reservation.ReservationId,
                        rejectionReason),
                    message.Envelope.TraceParent,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? DetermineRejectionReason(
        IReadOnlyCollection<InventoryReservationItem> requestedItems,
        IReadOnlyDictionary<string, StockItem> stockBySku)
    {
        if (requestedItems.Any(
                item => !stockBySku.ContainsKey(item.SkuId)))
        {
            return StockNotFoundReason;
        }

        return requestedItems.Any(
            item =>
                stockBySku[item.SkuId].Available
                < item.Quantity)
            ? InsufficientStockReason
            : null;
    }

    private static void ValidateMessage(
        ReserveInventoryMessage message)
    {
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
                "ReserveInventory payload OrderId must match envelope AggregateId.",
                nameof(message));
        }

        if (message.Payload.ExpiresAtUtc.Offset
            != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Reservation expiry must use UTC offset.",
                nameof(message));
        }
    }

    private static void EnsureEquivalent(
        InventoryReservation existing,
        ReserveInventoryV1 payload)
    {
        if (existing.OrderId != payload.OrderId
            || existing.ExpiresAtUtc != payload.ExpiresAtUtc
            || existing.Items.Count != payload.Items.Count)
        {
            throw new InvalidOperationException(
                "ReservationId was already used with a conflicting logical request.");
        }

        var existingItems = existing.Items
            .OrderBy(
                static item => item.SkuId,
                StringComparer.Ordinal)
            .ToArray();

        var incomingItems = payload.Items
            .OrderBy(
                static item => item.SkuId,
                StringComparer.Ordinal)
            .ToArray();

        for (var index = 0;
             index < existingItems.Length;
             index++)
        {
            if (!string.Equals(
                    existingItems[index].SkuId,
                    incomingItems[index].SkuId,
                    StringComparison.Ordinal)
                || existingItems[index].Quantity
                    != incomingItems[index].Quantity)
            {
                throw new InvalidOperationException(
                    "ReservationId was already used with a conflicting logical request.");
            }
        }
    }
}
