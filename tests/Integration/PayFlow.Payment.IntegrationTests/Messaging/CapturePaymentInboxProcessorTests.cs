using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Capture;
using PayFlow.Payment.Application.Messaging;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Messaging;
using PayFlow.Payment.Infrastructure.Persistence;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.IntegrationTests.Infrastructure;

namespace PayFlow.Payment.IntegrationTests.Messaging;

public sealed class CapturePaymentInboxProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 9, 27, 0, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task CaptureCommandCreatesProcessingPaymentAndStableProviderOperation()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await ProcessAsync(
            orderId,
            paymentId,
            Guid.NewGuid(),
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        var payment =
            await dbContext.Payments
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.PaymentId == paymentId,
                    cancellationToken);

        Assert.Equal(
            PaymentStatus.Processing.ToString(),
            payment.Status);

        var operation =
            await dbContext.ProviderOperations
                .AsNoTracking()
                .SingleAsync(
                    entity =>
                        entity.BusinessOperationId == paymentId,
                    cancellationToken);

        Assert.Equal(
            ProviderOperationStatus.Pending.ToString(),
            operation.Status);
        Assert.Equal(
            $"payment:{paymentId:D}:capture:v1",
            operation.ProviderIdempotencyKey);
    }

    [Fact]
    public async Task SamePaymentIdWithNewMessageIdDoesNotCreateSecondLogicalCapture()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await ProcessAsync(
            orderId,
            paymentId,
            Guid.NewGuid(),
            cancellationToken);

        await ProcessAsync(
            orderId,
            paymentId,
            Guid.NewGuid(),
            cancellationToken);

        await using var dbContext =
            fixture.CreateDbContext();

        Assert.Equal(
            1,
            await dbContext.Payments
                .AsNoTracking()
                .CountAsync(
                    entity => entity.PaymentId == paymentId,
                    cancellationToken));

        Assert.Equal(
            1,
            await dbContext.ProviderOperations
                .AsNoTracking()
                .CountAsync(
                    entity =>
                        entity.BusinessOperationId == paymentId,
                    cancellationToken));
    }

    private async Task ProcessAsync(
        Guid orderId,
        Guid paymentId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        var processor =
            new CapturePaymentInboxProcessor(
                dbContext,
                new InboxMessageRepository(dbContext),
                new CapturePaymentMessageHandler(
                    new PaymentRepository(dbContext),
                    new ProviderOperationRepository(dbContext),
                    new PaymentEfUnitOfWork(dbContext)));

        await processor.ProcessAsync(
            new ConsumedCapturePaymentMessage(
                new CapturePaymentMessage(
                    new IntegrationMessageEnvelope(
                        messageId,
                        "CapturePayment.v1",
                        1,
                        orderId,
                        orderId,
                        null,
                        OccurredAtUtc,
                        "Saga",
                        null),
                    new CapturePaymentV1(
                        orderId,
                        paymentId,
                        35m,
                        "USD")),
                "payments.commands",
                0,
                1,
                OccurredAtUtc),
            OccurredAtUtc.AddSeconds(1),
            cancellationToken);
    }
}
