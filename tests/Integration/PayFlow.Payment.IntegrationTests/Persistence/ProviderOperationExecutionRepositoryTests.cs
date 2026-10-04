using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Domain.ProviderOperations;
using PayFlow.Payment.Infrastructure.Persistence.Repositories;
using PayFlow.Payment.IntegrationTests.Infrastructure;
using PaymentAggregate = PayFlow.Payment.Domain.Payments.Payment;

namespace PayFlow.Payment.IntegrationTests.Persistence;

public sealed class ProviderOperationExecutionRepositoryTests(
    PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 27, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClaimPersistsProcessingAttemptBeforeProviderCall()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await ClearProviderOperationsAsync(cancellationToken);

        var paymentId = Guid.NewGuid();

        await SeedAsync(
            paymentId,
            cancellationToken);

        var claimAtUtc =
            CreatedAtUtc.AddSeconds(1);

        await using (var dbContext =
            fixture.CreateDbContext())
        {
            var workItem =
                await new ProviderOperationExecutionRepository(
                        dbContext)
                    .ClaimNextCaptureAsync(
                        claimAtUtc,
                        claimAtUtc.AddMinutes(-1),
                        cancellationToken);

            Assert.NotNull(workItem);
            Assert.Equal(paymentId, workItem.PaymentId);
            Assert.Equal(1, workItem.AttemptCount);
            Assert.Equal(
                $"payment:{paymentId:D}:capture:v1",
                workItem.ProviderIdempotencyKey);
        }

        await using var verificationDbContext =
            fixture.CreateDbContext();

        var persisted =
            await verificationDbContext.ProviderOperations
                .AsNoTracking()
                .SingleAsync(
                    operation =>
                        operation.BusinessOperationId
                        == paymentId,
                    cancellationToken);

        Assert.Equal(
            ProviderOperationStatus.Processing.ToString(),
            persisted.Status);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(claimAtUtc, persisted.LastAttemptAtUtc);
    }

    [Fact]
    public async Task StaleProcessingAttemptCanBeReclaimedWithSameIdempotencyKey()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await ClearProviderOperationsAsync(cancellationToken);

        var paymentId = Guid.NewGuid();

        await SeedAsync(
            paymentId,
            cancellationToken);

        string firstKey;

        await using (var firstDbContext =
            fixture.CreateDbContext())
        {
            var first =
                await new ProviderOperationExecutionRepository(
                        firstDbContext)
                    .ClaimNextCaptureAsync(
                        CreatedAtUtc.AddSeconds(1),
                        CreatedAtUtc.AddMinutes(-1),
                        cancellationToken);

            Assert.NotNull(first);
            firstKey = first.ProviderIdempotencyKey;
        }

        await using (var immediateDbContext =
            fixture.CreateDbContext())
        {
            var immediate =
                await new ProviderOperationExecutionRepository(
                        immediateDbContext)
                    .ClaimNextCaptureAsync(
                        CreatedAtUtc.AddSeconds(2),
                        CreatedAtUtc,
                        cancellationToken);

            Assert.Null(immediate);
        }

        await using (var staleDbContext =
            fixture.CreateDbContext())
        {
            var reclaimed =
                await new ProviderOperationExecutionRepository(
                        staleDbContext)
                    .ClaimNextCaptureAsync(
                        CreatedAtUtc.AddMinutes(2),
                        CreatedAtUtc.AddMinutes(1),
                        cancellationToken);

            Assert.NotNull(reclaimed);
            Assert.Equal(2, reclaimed.AttemptCount);
            Assert.Equal(
                firstKey,
                reclaimed.ProviderIdempotencyKey);
        }
    }

    private async Task ClearProviderOperationsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            fixture.CreateDbContext();

        _ = await dbContext.ProviderOperations
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task SeedAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment =
            PaymentAggregate.Create(
                paymentId,
                Guid.NewGuid(),
                35m,
                "USD",
                CreatedAtUtc);

        payment.StartProcessing(CreatedAtUtc);

        var operation =
            ProviderOperation.CreateCapture(
                Guid.NewGuid(),
                paymentId,
                CreatedAtUtc);

        await using var dbContext =
            fixture.CreateDbContext();

        await new PaymentRepository(dbContext)
            .AddAsync(payment, cancellationToken);

        await new ProviderOperationRepository(dbContext)
            .AddAsync(operation, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
