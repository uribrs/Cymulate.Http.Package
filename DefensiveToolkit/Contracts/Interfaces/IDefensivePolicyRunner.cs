namespace DefensiveToolkit.Contracts.Interfaces;

public interface IDefensivePolicyRunner
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}