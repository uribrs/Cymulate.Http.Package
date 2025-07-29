using DefensiveToolkit.Contracts.Interfaces;

namespace DefensiveToolkit.Policies;

public sealed class NoOpPolicy : IResiliencePolicy
{
    private readonly string _name;

    public NoOpPolicy(Type policyType)
    {
        _name = policyType.Name;
    }


    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        return await operation(cancellationToken);
    }
}