using DefensiveToolkit.Contracts;
using DefensiveToolkit.Contracts.Options;
using DefensiveToolkit.Telemetry;
using Microsoft.Extensions.Logging;

namespace DefensiveToolkit.Policies;

public class TimeoutPolicy : IResiliencePolicy
{
    private readonly TimeoutOptions _options;
    private readonly ILogger<TimeoutPolicy>? _logger;
    public static readonly IResiliencePolicy NoOp = new NoOpPolicy(typeof(RetryPolicy));

    public TimeoutPolicy(TimeoutOptions options, ILogger<TimeoutPolicy>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var activity = DefensiveDiagnostics.StartPolicyActivity("TimeoutPolicy", "ExecuteAsync");
        DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.TimeoutThreshold, _options.Timeout.TotalMilliseconds);

        using var timeoutCts = new CancellationTokenSource(_options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var result = await operation(linkedCts.Token).ConfigureAwait(false);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);
            return result;
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            DefensiveDiagnostics.RecordFailure(activity, ex);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);

            _logger?.LogWarning("[TimeoutPolicy] Operation timed out after {Timeout}ms", _options.Timeout.TotalMilliseconds);

            throw new TimeoutException($"Operation exceeded timeout of {_options.Timeout.TotalMilliseconds}ms", ex);
        }
        catch (Exception ex)
        {
            DefensiveDiagnostics.RecordFailure(activity, ex);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);

            _logger?.LogError(ex, "[TimeoutPolicy] Operation failed due to an unhandled exception.");
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }
}