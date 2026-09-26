using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Domain.Payments;
using PayFlow.Payment.Infrastructure.Persistence.Mappers;

namespace PayFlow.Payment.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;

    public PaymentRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        await _dbContext.Payments.AddAsync(
            PaymentEntityMapper.ToEntity(payment),
            cancellationToken);
    }

    public async Task<Payment?> GetByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Payment ID cannot be empty.",
                nameof(paymentId));
        }

        var entity = await _dbContext.Payments
            .SingleOrDefaultAsync(
                payment => payment.PaymentId == paymentId,
                cancellationToken);

        return entity is null
            ? null
            : PaymentEntityMapper.ToDomain(entity);
    }

    public Task ApplyAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = _dbContext.Payments.Local
            .SingleOrDefault(
                tracked => tracked.PaymentId == payment.PaymentId)
            ?? throw new InvalidOperationException(
                "Payment must be loaded by this repository before applying a transition.");

        entity.Status = payment.Status.ToString();
        entity.UpdatedAtUtc = payment.UpdatedAtUtc;
        entity.CapturedAtUtc = payment.CapturedAtUtc;
        entity.FailureReasonCode = payment.FailureReasonCode;
        entity.Version = payment.Version;

        return Task.CompletedTask;
    }
}
