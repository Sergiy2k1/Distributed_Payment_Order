using System.Collections.ObjectModel;

namespace PayFlow.Saga.Domain.Checkout;

public sealed class CheckoutSaga
{
    private CheckoutSaga(
        Guid orderId,
        Guid customerId,
        ReadOnlyCollection<CheckoutSagaItem> items,
        string currency,
        decimal totalAmount,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        Currency = currency;
        TotalAmount = totalAmount;
        Status = CheckoutSagaStatus.Started;
        StartedAtUtc = startedAtUtc;
        UpdatedAtUtc = startedAtUtc;
        DeadlineAtUtc = deadlineAtUtc;
        RetryCount = 0;
        NextAttemptAtUtc = null;
        LastTechnicalErrorCode = null;
        LastTechnicalErrorMessage = null;
        Version = 0;
    }

    public Guid OrderId { get; }

    public Guid CustomerId { get; }

    public IReadOnlyList<CheckoutSagaItem> Items { get; }

    public string Currency { get; }

    public decimal TotalAmount { get; }

    public CheckoutSagaStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset DeadlineAtUtc { get; }

    public int RetryCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LastTechnicalErrorCode { get; private set; }

    public string? LastTechnicalErrorMessage { get; private set; }

    public long Version { get; private set; }

    public static CheckoutSaga Start(
        Guid orderId,
        Guid customerId,
        IEnumerable<CheckoutSagaItem> items,
        string currency,
        decimal totalAmount,
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order ID cannot be empty.",
                nameof(orderId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Customer ID cannot be empty.",
                nameof(customerId));
        }

        ArgumentNullException.ThrowIfNull(items);

        EnsureUtc(
            startedAtUtc,
            nameof(startedAtUtc));
        EnsureUtc(
            deadlineAtUtc,
            nameof(deadlineAtUtc));

        if (deadlineAtUtc <= startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadlineAtUtc),
                deadlineAtUtc,
                "Checkout deadline must be later than the start time.");
        }

        var itemArray = items.ToArray();

        if (itemArray.Length == 0)
        {
            throw new ArgumentException(
                "Checkout Saga must contain at least one item.",
                nameof(items));
        }

        var normalizedCurrency =
            CheckoutSagaItem.NormalizeCurrency(
                currency);

        if (itemArray.Any(
                item => !string.Equals(
                    item.Currency,
                    normalizedCurrency,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "All checkout items must use the Saga currency.",
                nameof(items));
        }

        if (totalAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalAmount),
                totalAmount,
                "Total amount must be greater than zero.");
        }

        var calculatedTotal =
            itemArray.Sum(item => item.LineTotal);

        if (calculatedTotal != totalAmount)
        {
            throw new ArgumentException(
                "Total amount must equal the immutable item snapshot total.",
                nameof(totalAmount));
        }

        return new CheckoutSaga(
            orderId,
            customerId,
            Array.AsReadOnly(itemArray),
            normalizedCurrency,
            totalAmount,
            startedAtUtc,
            deadlineAtUtc);
    }

    private static void EnsureUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset.",
                parameterName);
        }
    }
}
