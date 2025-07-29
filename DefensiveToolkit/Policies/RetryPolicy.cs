using Polly;
using System.Diagnostics;
using DefensiveToolkit.Contracts.Options;
using DefensiveToolkit.Telemetry;
using Microsoft.Extensions.Logging;
using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Contracts.Interfaces;

namespace DefensiveToolkit.Policies;

public class RetryPolicy : IResiliencePolicy
{
    private readonly RetryOptions _options;
    private readonly ILogger<RetryPolicy>? _logger;
    public static readonly IResiliencePolicy NoOp = new NoOpPolicy(typeof(RetryPolicy));

    public RetryPolicy(RetryOptions options, ILogger<RetryPolicy>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        _logger?.LogInformation("[RetryPolicy] Using backoff strategy: {Strategy}", _options.BackoffStrategy);
        _logger?.LogInformation("[RetryPolicy] Using delay: {Delay}", _options.Delay);
        _logger?.LogInformation("[RetryPolicy] Using max attempts: {MaxAttempts}", _options.MaxAttempts);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var retryAttempts = 0;

        var policy = Policy
            .Handle<Exception>(ex => _options.ShouldRetry?.Invoke(ex, null) ?? false)
            .OrResult<T>(result => _options.ShouldRetry?.Invoke(null, result) ?? false)
            .WaitAndRetryAsync(
                retryCount: Math.Max(0, _options.MaxAttempts - 1),
                sleepDurationProvider: GetDelayStrategy(),
                onRetry: (outcome, timespan, retryCount, _) =>
                {
                    retryAttempts = retryCount;
                    var error = outcome.Exception?.Message ?? outcome.Result?.ToString() ?? "unknown";

                    var activity = Activity.Current;
                    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RetryCount, retryCount);

                    _logger?.LogWarning("[RetryPolicy] Retry {RetryCount}/{Max} after {Delay}ms due to: {Reason}",
                        retryCount, _options.MaxAttempts, timespan.TotalMilliseconds, error);
                });
        var activity = DefensiveDiagnostics.StartPolicyActivity("RetryPolicy", "ExecuteAsync");

        try
        {
            var result = await policy.ExecuteAsync(ct => operation(ct), cancellationToken);

            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);

            if (retryAttempts > 0)
            {
                _logger?.LogInformation("[RetryPolicy] Operation succeeded after {RetryCount} retries.", retryAttempts);
            }

            return result;
        }
        catch (Exception ex)
        {
            DefensiveDiagnostics.RecordFailure(activity, ex);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);

            _logger?.LogError(ex, "[RetryPolicy] Operation failed after {RetryCount} retries.", retryAttempts);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private Func<int, TimeSpan> GetDelayStrategy()
    {
        return _options.BackoffStrategy switch
        {
            RetryBackoff.Fixed => _ => _options.Delay,
            RetryBackoff.Exponential => attempt => TimeSpan.FromMilliseconds(_options.Delay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
            RetryBackoff.ExponentialWithJitter => attempt =>
            {
                var baseDelay = _options.Delay.TotalMilliseconds * Math.Pow(2, attempt - 1);
                var jitter = Random.Shared.NextDouble() * baseDelay * 0.5;
                return TimeSpan.FromMilliseconds(baseDelay + jitter);
            },
            _ => _ => _options.Delay
        };
    }
}