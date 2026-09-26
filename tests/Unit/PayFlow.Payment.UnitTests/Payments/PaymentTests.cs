using PayFlow.Payment.Domain.Payments;

namespace PayFlow.Payment.UnitTests.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 26, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateStartsInCreatedState()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            35m,
            "usd",
            CreatedAtUtc);

        Assert.Equal(PaymentStatus.Created, payment.Status);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(0, payment.Version);
    }

    [Fact]
    public void ProcessingCanBecomeCaptured()
    {
        var payment = CreatePayment();

        payment.StartProcessing(CreatedAtUtc.AddSeconds(1));
        payment.MarkCaptured(CreatedAtUtc.AddSeconds(2));

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(2, payment.Version);
    }

    [Fact]
    public void ProcessingCanBecomeFailed()
    {
        var payment = CreatePayment();

        payment.StartProcessing(CreatedAtUtc.AddSeconds(1));
        payment.MarkFailed(
            "DECLINED",
            CreatedAtUtc.AddSeconds(2));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("DECLINED", payment.FailureReasonCode);
    }

    private static Payment CreatePayment() =>
        Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            35m,
            "USD",
            CreatedAtUtc);
}
