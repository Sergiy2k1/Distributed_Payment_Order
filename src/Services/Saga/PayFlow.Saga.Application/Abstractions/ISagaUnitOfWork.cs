namespace PayFlow.Saga.Application.Abstractions;

public interface ISagaUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
