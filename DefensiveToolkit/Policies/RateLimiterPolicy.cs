using System.Diagnostics;
using System.Threading.RateLimiting;
using DefensiveToolkit.Contracts;
using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Contracts.Options;
using DefensiveToolkit.Telemetry;
using Microsoft.Extensions.Logging;
using Polly.RateLimit;

namespace DefensiveToolkit.Policies;

public sealed class RateLimiterPolicy : IResiliencePolicy, IAsyncDisposable
{
    private readonly RateLimiter _limiter;
    private readonly ILogger<RateLimiterPolicy>? _logger;
    private const int WarnThresholdMs = 500;

    public static readonly IResiliencePolicy NoOp = new NoOpPolicy(typeof(RateLimiterPolicy));

    public RateLimiterPolicy(RateLimiterOptions options, ILogger<RateLimiterPolicy>? logger = null)
    {
        _logger = logger;
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();
        _limiter = BuildLimiter(options);

        LogLimiterConfiguration(options);
    }

    private void LogLimiterConfiguration(RateLimiterOptions options)
    {
        _logger?.LogInformation("[RateLimiterPolicy] Using limiter kind: {Kind}", options.Kind);

        switch (options.Kind)
        {
            case RateLimiterKind.FixedWindow:
                var fw = options.FixedWindow!;
                _logger?.LogInformation("[RateLimiterPolicy] FixedWindow: {PermitLimit} permits / {Window} window, queue: {QueueLimit}",
                    fw.PermitLimit, fw.Window, fw.QueueLimit);
                break;

            case RateLimiterKind.SlidingWindow:
                var sw = options.SlidingWindow!;
                _logger?.LogInformation("[RateLimiterPolicy] SlidingWindow: {PermitLimit} permits / {Window} window with {Segments} segments, queue: {QueueLimit}",
                    sw.PermitLimit, sw.Window, sw.SegmentsPerWindow, sw.QueueLimit);
                break;

            case RateLimiterKind.TokenBucket:
                var tb = options.TokenBucket!;
                _logger?.LogInformation("[RateLimiterPolicy] TokenBucket: {RefillRate}/s, capacity {Capacity}, queue: {QueueLimit}",
                    tb.TokenBucketRefillRate, tb.TokenBucketCapacity, tb.QueueLimit);
                break;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var activity = DefensiveDiagnostics.StartPolicyActivity("RateLimiterPolicy", "ExecuteAsync");
        var stopwatch = Stopwatch.StartNew();
        RateLimitLease? lease = null;

        try
        {
            lease = await _limiter.AcquireAsync(1, cancellationToken);

            if (!lease.IsAcquired)
            {
                _logger?.LogWarning("[RateLimiter] Request rejected — queue limit exceeded.");
                DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Rejected);
                throw new RateLimitRejectedException("Request was rejected by rate limiter.");
            }

            stopwatch.Stop();
            var waitTime = stopwatch.ElapsedMilliseconds;
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterWait, waitTime);

            if (waitTime > WarnThresholdMs)
            {
                _logger?.LogWarning("[RateLimiter] Request waited {WaitTime}ms before acquiring permit.", waitTime);
            }

            var result = await operation(cancellationToken);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);
            return result;
        }
        catch (RateLimitRejectedException ex)
        {
            _logger?.LogWarning(ex, "[RateLimiter] Request was force-rejected by limiter.");
            DefensiveDiagnostics.RecordFailure(activity, ex);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Rejected);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RateLimiter] Operation failed after acquiring permit.");
            DefensiveDiagnostics.RecordFailure(activity, ex);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);
            throw;
        }
        finally
        {
            lease?.Dispose();
            activity?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_limiter is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
    }

    private static RateLimiter BuildLimiter(RateLimiterOptions options)
    {
        return options.Kind switch
        {
            RateLimiterKind.FixedWindow => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.FixedWindow!.PermitLimit,
                Window = options.FixedWindow.Window,
                QueueLimit = options.FixedWindow.QueueLimit,
                QueueProcessingOrder = options.FixedWindow.QueueProcessingOrder,
                AutoReplenishment = options.FixedWindow.AutoReplenishment
            }),

            RateLimiterKind.SlidingWindow => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = options.SlidingWindow!.PermitLimit,
                Window = options.SlidingWindow.Window,
                SegmentsPerWindow = options.SlidingWindow.SegmentsPerWindow,
                QueueLimit = options.SlidingWindow.QueueLimit,
                QueueProcessingOrder = options.SlidingWindow.QueueProcessingOrder,
                AutoReplenishment = options.SlidingWindow.AutoReplenishment
            }),

            RateLimiterKind.TokenBucket => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = options.TokenBucket!.TokenBucketCapacity,
                TokensPerPeriod = (int)Math.Round(options.TokenBucket.TokenBucketRefillRate),
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = options.TokenBucket.QueueLimit,
                QueueProcessingOrder = options.TokenBucket.QueueProcessingOrder,
                AutoReplenishment = options.TokenBucket.AutoReplenishment
            }),

            _ => throw new ArgumentOutOfRangeException(nameof(options.Kind), $"Unknown limiter kind: {options.Kind}")
        };
    }
}