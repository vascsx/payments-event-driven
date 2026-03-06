namespace Payments.EventDriven.Application.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Executes the given action within a transaction, using the configured execution strategy.
    /// This is required when using retry execution strategies like NpgsqlRetryingExecutionStrategy.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Executes the given action within a transaction, using the configured execution strategy.
    /// This is required when using retry execution strategies like NpgsqlRetryingExecutionStrategy.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);
}
