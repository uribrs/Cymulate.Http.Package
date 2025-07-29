using DefensiveToolkit.Contracts.Interfaces;
using DefensiveToolkit.Telemetry;
using Microsoft.Extensions.Logging;

namespace DefensiveToolkit.Core;

public class DefensivePolicyRunner : IDefensivePolicyRunner
{
    private readonly IResiliencePolicy? _rateLimiter;
    private readonly IResiliencePolicy? _timeout;
    private readonly IResiliencePolicy? _retry;
    private readonly IResiliencePolicy? _circuitBreaker;
    private readonly ILogger<DefensivePolicyRunner>? _logger;

    public DefensivePolicyRunner(
        IResiliencePolicy? rateLimiter = null,
        IResiliencePolicy? timeout = null,
        IResiliencePolicy? retry = null,
        IResiliencePolicy? circuitBreaker = null,
        ILogger<DefensivePolicyRunner>? logger = null)
    {
        _rateLimiter = rateLimiter;
        _timeout = timeout;
        _retry = retry;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var activity = DefensiveDiagnostics.StartPolicyActivity("DefensivePolicyRunner", "ExecuteAsync");

        try
        {
            _logger?.LogDebug("[Runner] Executing policy chain: {Chain}",
                GetPolicyChainSummary());

            var result = await ExecuteChainedAsync(operation, cancellationToken);

            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);
            return result;
        }
        catch (Exception ex)
        {
            DefensiveDiagnostics.RecordFailure(activity, ex);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);

            _logger?.LogError(ex, "[Runner] Operation failed through defensive policy chain.");
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private async Task<T> ExecuteChainedAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var current = operation;

        if (_circuitBreaker is not null)
        {
            var inner = current;
            current = ct => _circuitBreaker.ExecuteAsync(inner, ct);
        }

        if (_retry is not null)
        {
            var inner = current;
            current = ct => _retry.ExecuteAsync(inner, ct);
        }

        if (_timeout is not null)
        {
            var inner = current;
            current = ct => _timeout.ExecuteAsync(inner, ct);
        }

        if (_rateLimiter is not null)
        {
            var inner = current;
            current = ct => _rateLimiter.ExecuteAsync(inner, ct);
        }

        return await current(cancellationToken);
    }

    private string GetPolicyChainSummary()
    {
        var chain = new List<string>();

        if (_rateLimiter != null) chain.Add("RateLimiter");
        if (_timeout != null) chain.Add("Timeout");
        if (_retry != null) chain.Add("Retry");
        if (_circuitBreaker != null) chain.Add("CircuitBreaker");

        return chain.Count > 0 ? string.Join(" → ", chain) : "None";
    }
}