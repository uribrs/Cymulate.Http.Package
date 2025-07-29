namespace DefensiveToolkit.Contracts.Interfaces;

public interface IResiliencePolicy
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}