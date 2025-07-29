using Polly;
using Polly.CircuitBreaker;
using System.Diagnostics;
using DefensiveToolkit.Contracts.Options;
using DefensiveToolkit.Telemetry;
using Microsoft.Extensions.Logging;
using DefensiveToolkit.Contracts.Interfaces;

namespace DefensiveToolkit.Policies;

public class CircuitBreakerPolicy : IResiliencePolicy
{
    private readonly ILogger<CircuitBreakerPolicy>? _logger;
    private readonly AsyncCircuitBreakerPolicy<object?> _policy;
    public static readonly IResiliencePolicy NoOp = new NoOpPolicy(typeof(RetryPolicy));

    public CircuitBreakerPolicy(CircuitBreakerOptions options, ILogger<CircuitBreakerPolicy>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;

        _policy = Policy<object?>
            .Handle<Exception>(ex => options.ShouldHandleException?.Invoke(ex) ?? true)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: options.FailureThreshold,
                durationOfBreak: options.BreakDuration,
                onBreak: (outcome, breakDelay) =>
                {
                    var activity = Activity.Current;
                    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.CircuitBreakerState, "Open");

                    if (outcome.Exception is { } ex)
                        DefensiveDiagnostics.RecordFailure(activity, ex);

                    _logger?.LogWarning("[CircuitBreaker] Opened for {BreakDelay} due to: {Error}",
                        breakDelay, outcome.Exception?.Message ?? "unknown reason");
                },
                onReset: () =>
                {
                    var activity = Activity.Current;
                    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.CircuitBreakerState, "Closed");

                    _logger?.LogInformation("[CircuitBreaker] Reset: Circuit closed and normal operation resumed.");
                },
                onHalfOpen: () =>
                {
                    var activity = Activity.Current;
                    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.CircuitBreakerState, "HalfOpen");

                    _logger?.LogInformation("[CircuitBreaker] Half-open: Testing circuit recovery.");
                });
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var activity = DefensiveDiagnostics.StartPolicyActivity("CircuitBreakerPolicy", "ExecuteAsync");

        try
        {
            var result = await _policy.ExecuteAsync
                (
                async ct =>
                {
                    var res = await operation(ct);
                    return (object?)res;
                },
                cancellationToken);

            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);
            return (T)result!;
        }
        catch (Exception ex)
        {
            DefensiveDiagnostics.RecordFailure(activity, ex);

            var state = _policy.CircuitState.ToString();
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.CircuitBreakerState, state);

            _logger?.LogWarning("[CircuitBreaker] Operation failed during state {CircuitState}: {Message}",
                state, ex.Message);

            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }
}