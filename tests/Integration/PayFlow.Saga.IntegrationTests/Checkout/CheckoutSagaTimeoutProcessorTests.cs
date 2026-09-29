using Microsoft.EntityFrameworkCore;
using PayFlow.Saga.Application.Checkout;
using PayFlow.Saga.Domain.Checkout;
using PayFlow.Saga.Infrastructure.Messaging.Outbox;
using PayFlow.Saga.Infrastructure.Persistence;
using PayFlow.Saga.Infrastructure.Persistence.Repositories;
using PayFlow.Saga.IntegrationTests.Infrastructure;

namespace PayFlow.Saga.IntegrationTests.Checkout;

public sealed class CheckoutSagaTimeoutProcessorTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 9, 30, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task OverdueBatchStartsSafePostCaptureCompensationAndDefersAmbiguousPaymentState()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var postCapture =
            CreateWaitingForInventoryCommitSaga(
                NowUtc.AddHours(-1),
                NowUtc.AddMinutes(-10));

        var ambiguousPayment =
            CreateWaitingForPaymentSaga(
                NowUtc.AddHours(-1),
                NowUtc.AddMinutes(-5));

        var futurePostCapture =
            CreateWaitingForInventoryCommitSaga(
                NowUtc.AddMinutes(-5),
                NowUtc.AddMinutes(20));

        await using (var seedDbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(
                    seedDbContext);

            await repository.AddAsync(
                postCapture,
                cancellationToken);
            await repository.AddAsync(
                ambiguousPayment,
                cancellationToken);
            await repository.AddAsync(
                futurePostCapture,
                cancellationToken);

            await seedDbContext.SaveChangesAsync(
                cancellationToken);
        }

        IReadOnlyList<CheckoutSagaTimeoutOutcome> outcomes;

        await using (var processDbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(
                    processDbContext);

            var processor =
                new CheckoutSagaTimeoutProcessor(
                    repository,
                    new PostCaptureCompensationStarter(
                        repository,
                        new SagaOutboxWriter(
                            processDbContext),
                        new SagaEfUnitOfWork(
                            processDbContext)));

            outcomes =
                await processor.ProcessBatchAsync(
                    NowUtc,
                    batchSize: 100,
                    cancellationToken);
        }

        Assert.Contains(
            outcomes,
            outcome =>
                outcome.OrderId == postCapture.OrderId
                && outcome.Action
                == CheckoutSagaTimeoutAction.PostCaptureCompensationStarted);

        Assert.Contains(
            outcomes,
            outcome =>
                outcome.OrderId == ambiguousPayment.OrderId
                && outcome.Action
                == CheckoutSagaTimeoutAction.RequiresReconciliation);

        Assert.DoesNotContain(
            outcomes,
            outcome =>
                outcome.OrderId
                == futurePostCapture.OrderId);

        await using (var verificationDbContext =
            fixture.CreateDbContext())
        {
            var compensated =
                await verificationDbContext.CheckoutSagas
                    .AsNoTracking()
                    .SingleAsync(
                        saga =>
                            saga.OrderId
                            == postCapture.OrderId,
                        cancellationToken);

            Assert.Equal(
                CheckoutSagaStatus.CompensatingPayment.ToString(),
                compensated.Status);
            Assert.NotNull(
                compensated.RefundId);
            Assert.Equal(
                PostCaptureCompensationMode.ReleaseReservedInventory.ToString(),
                compensated.PostCaptureCompensationMode);

            var ambiguous =
                await verificationDbContext.CheckoutSagas
                    .AsNoTracking()
                    .SingleAsync(
                        saga =>
                            saga.OrderId
                            == ambiguousPayment.OrderId,
                        cancellationToken);

            Assert.Equal(
                CheckoutSagaStatus.WaitingForPayment.ToString(),
                ambiguous.Status);

            Assert.Equal(
                1,
                await verificationDbContext.OutboxMessages
                    .AsNoTracking()
                    .CountAsync(
                        message =>
                            message.AggregateId
                            == postCapture.OrderId
                            && message.MessageType
                            == "RefundPayment.v1",
                        cancellationToken));

            Assert.Equal(
                0,
                await verificationDbContext.OutboxMessages
                    .AsNoTracking()
                    .CountAsync(
                        message =>
                            message.AggregateId
                            == ambiguousPayment.OrderId,
                        cancellationToken));
        }

        await using (var replayDbContext =
            fixture.CreateDbContext())
        {
            var repository =
                new CheckoutSagaRepository(
                    replayDbContext);

            var processor =
                new CheckoutSagaTimeoutProcessor(
                    repository,
                    new PostCaptureCompensationStarter(
                        repository,
                        new SagaOutboxWriter(
                            replayDbContext),
                        new SagaEfUnitOfWork(
                            replayDbContext)));

            var replayOutcomes =
                await processor.ProcessBatchAsync(
                    NowUtc.AddMinutes(1),
                    batchSize: 100,
                    cancellationToken);

            Assert.Contains(
                replayOutcomes,
                outcome =>
                    outcome.OrderId
                    == postCapture.OrderId
                    && outcome.Action
                    == CheckoutSagaTimeoutAction.RecoveryAlreadyInProgress);
        }

        await using var finalDbContext =
            fixture.CreateDbContext();

        Assert.Equal(
            1,
            await finalDbContext.OutboxMessages
                .AsNoTracking()
                .CountAsync(
                    message =>
                        message.AggregateId
                        == postCapture.OrderId
                        && message.MessageType
                        == "RefundPayment.v1",
                    cancellationToken));
    }

    private static CheckoutSaga CreateWaitingForPaymentSaga(
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        var saga =
            CheckoutSaga.Start(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateItems(),
                "USD",
                35m,
                startedAtUtc,
                deadlineAtUtc);

        var reservationId =
            Guid.NewGuid();

        saga.BeginInventoryReservation(
            reservationId,
            startedAtUtc.AddSeconds(1),
            deadlineAtUtc);
        saga.ConfirmInventoryReserved(
            reservationId,
            Guid.NewGuid(),
            startedAtUtc.AddSeconds(2));

        return saga;
    }

    private static CheckoutSaga CreateWaitingForInventoryCommitSaga(
        DateTimeOffset startedAtUtc,
        DateTimeOffset deadlineAtUtc)
    {
        var saga =
            CreateWaitingForPaymentSaga(
                startedAtUtc,
                deadlineAtUtc);

        saga.ConfirmPaymentCaptured(
            saga.PaymentId
                ?? throw new InvalidOperationException(
                    "Test Saga PaymentId is missing."),
            startedAtUtc.AddSeconds(3));

        return saga;
    }

    private static CheckoutSagaItem[] CreateItems()
    {
        return
        [
            CheckoutSagaItem.Create(
                "SKU-TIMEOUT-001",
                2,
                10m,
                "USD"),
            CheckoutSagaItem.Create(
                "SKU-TIMEOUT-002",
                3,
                5m,
                "USD")
        ];
    }
}
