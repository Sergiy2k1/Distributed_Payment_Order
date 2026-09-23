namespace PayFlow.Order.Application.Abstractions;

public interface IUnitOfWork
{
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
