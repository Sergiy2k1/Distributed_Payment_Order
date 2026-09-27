using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Messaging;
using PayFlow.Payment.Application.Refund;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Infrastructure.Messaging;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.IntegrationTests.Infrastructure;
using PaymentAggregate = PayFlow.Payment.Domain.Payments.Payment;

namespace PayFlow.Payment.IntegrationTests.Messaging;

public sealed class RefundPaymentInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefundCommandMovesCapturedPaymentAndCreatesStableProviderOperation()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        await SeedCapturedPaymentAsync(
            orderId,
            paymentId,
            cancellationToken);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var processor =
                new RefundPaymentInboxProcessor(
                    dbContext,
                    new InboxMessageRepository(dbContext),
                    new RefundPaymentMessageHandler(
                        new PaymentRepository(dbContext),
                        new ProviderOperationRepository(dbContext),
                        new PaymentEfUnitOfWork(dbContext)));

            await processor.ProcessAsync(
                new ConsumedRefundPaymentMessage(
                    new RefundPaymentMessage(
                        new IntegrationMessageEnvelope(
                            Guid.NewGuid(),
                            "RefundPayment.v1",
                            1,
                            orderId,
                            orderId,
                            null,
                            CreatedAtUtc.AddSeconds(3),
                            "Saga",
                            null),
                        new RefundPaymentV1(
                            orderId,
                            paymentId,
                            refundId,
                            35m,
                            "USD",
                            "CHECKOUT_COMPENSATION")),
                    "payments.commands",
                    0,
                    20,
                    CreatedAtUtc.AddSeconds(3)),
                CreatedAtUtc.AddSeconds(4),
                cancellationToken);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var payment =
            await verificationDbContext.Payments
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.PaymentId == paymentId,
                    cancellationToken);

        Assert.Equal(
            PaymentStatus.RefundPending.ToString(),
            payment.Status);

        var operation =
            await verificationDbContext.ProviderOperations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.OperationType == "Refund"
                        && entity.BusinessOperationId == paymentId,
                    cancellationToken);

        Assert.Equal(refundId, operation.ProviderOperationId);
        Assert.Equal(
            $"payment:{paymentId:D}:refund:{refundId:D}:v1",
            operation.ProviderIdempotencyKey);
    }

    private async Task SeedCapturedPaymentAsync(
        Guid orderId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment =
            PaymentAggregate.Create(
                paymentId,
                orderId,
                35m,
                "USD",
                CreatedAtUtc);

        payment.StartProcessing(
            CreatedAtUtc.AddSeconds(1));
        payment.MarkCaptured(
            CreatedAtUtc.AddSeconds(2));

        await using var dbContext =
            fixture.CreateDbContext();

        await new PaymentRepository(dbContext)
            .AddAsync(payment, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
