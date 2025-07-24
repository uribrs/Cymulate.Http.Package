using System.Diagnostics;
using System.Threading.RateLimiting;
using DefensiveToolkit.Contracts;
using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Contracts.Options;
using DefensiveToolkit.Telemetry;
using Microsoft.Extensions.Logging;
using Polly.RateLimit;

namespace DefensiveToolkit.Policies;

public class RateLimiterPolicy : IResiliencePolicy, IAsyncDisposable
{
    private readonly RateLimiter _limiter;
    private readonly ILogger<RateLimiterPolicy>? _logger;
    public static readonly IResiliencePolicy NoOp = new NoOpPolicy(typeof(RetryPolicy));

    private const int WarnThresholdMs = 500; // customizable

    public RateLimiterPolicy(RateLimiterOptions options, ILogger<RateLimiterPolicy>? logger = null)
    {
        _logger = logger;
        ArgumentNullException.ThrowIfNull(options);

        // Validate options before building the limiter
        ValidateOptions(options);

        _limiter = BuildLimiter(options);

        _logger?.LogInformation("[RateLimiterPolicy] Using limiter kind: {Kind}", options.Kind);
        _logger?.LogInformation("[RateLimiterPolicy] Using permit limit: {PermitLimit}", options.PermitLimit);
        _logger?.LogInformation("[RateLimiterPolicy] Using window: {Window}", options.Window);
        _logger?.LogInformation("[RateLimiterPolicy] Using queue limit: {QueueLimit}", options.QueueLimit);
        _logger?.LogInformation("[RateLimiterPolicy] Using segments per window: {SegmentsPerWindow}", options.SegmentsPerWindow);
        _logger?.LogInformation("[RateLimiterPolicy] Using token bucket refill rate: {TokenBucketRefillRate}", options.TokenBucketRefillRate);
        _logger?.LogInformation("[RateLimiterPolicy] Using token bucket capacity: {TokenBucketCapacity}", options.TokenBucketCapacity);
    }

    private static void ValidateOptions(RateLimiterOptions options)
    {
        // Validate enum value
        if (!Enum.IsDefined(typeof(RateLimiterKind), options.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(options.Kind), options.Kind, "Invalid rate limiter kind");
        }

        // Validate all properties regardless of limiter type (strict validation)
        if (options.PermitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.PermitLimit), options.PermitLimit, "PermitLimit must be greater than 0");
        }

        if (options.QueueLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.QueueLimit), options.QueueLimit, "QueueLimit must be non-negative");
        }

        if (options.SegmentsPerWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SegmentsPerWindow), options.SegmentsPerWindow, "SegmentsPerWindow must be greater than 0");
        }

        if (options.TokenBucketCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TokenBucketCapacity), options.TokenBucketCapacity, "TokenBucketCapacity must be greater than 0");
        }

        if (options.TokenBucketRefillRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TokenBucketRefillRate), options.TokenBucketRefillRate, "TokenBucketRefillRate must be greater than 0");
        }

        if (options.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Window), options.Window, "Window must be greater than zero");
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
                PermitLimit = options.PermitLimit,
                Window = options.Window,
                QueueLimit = options.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }),

            RateLimiterKind.SlidingWindow => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = options.Window,
                SegmentsPerWindow = options.SegmentsPerWindow,
                QueueLimit = options.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }),

            RateLimiterKind.TokenBucket => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = options.TokenBucketCapacity,
                TokensPerPeriod = (int)Math.Round(options.TokenBucketRefillRate),
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = options.QueueLimit,
                AutoReplenishment = true
            }),

            _ => throw new ArgumentOutOfRangeException(nameof(options.Kind), $"Unknown limiter kind: {options.Kind}")
        };
    }
}