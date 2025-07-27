using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Contracts.Options.RateLimiterKindOptions;

namespace DefensiveToolkit.Contracts.Options;

public sealed class RateLimiterOptions
{
    public RateLimiterKind Kind { get; set; }

    public FixedWindowOptions? FixedWindow { get; set; }
    public SlidingWindowOptions? SlidingWindow { get; set; }
    public TokenBucketOptions? TokenBucket { get; set; }

    public void Validate()
    {
        switch (Kind)
        {
            case RateLimiterKind.FixedWindow:
                if (FixedWindow is null)
                    throw new InvalidOperationException("FixedWindow config is required.");
                break;

            case RateLimiterKind.SlidingWindow:
                if (FixedWindow is null)
                    throw new InvalidOperationException("SlidingWindow requires FixedWindow base config.");
                if (SlidingWindow is null)
                    throw new InvalidOperationException("SlidingWindow config is required.");
                break;

            case RateLimiterKind.TokenBucket:
                if (TokenBucket is null)
                    throw new InvalidOperationException("TokenBucket config is required.");
                break;

            default:
                throw new NotSupportedException($"Unsupported RateLimiterKind: {Kind}");
        }
    }

    public override string ToString()
    {
        return Kind switch
        {
            RateLimiterKind.FixedWindow =>
                $"FixedWindow: {FixedWindow?.PermitLimit} permits / {FixedWindow?.Window.TotalSeconds}s, queue {FixedWindow?.QueueLimit}",

            RateLimiterKind.SlidingWindow =>
                $"SlidingWindow: {FixedWindow?.PermitLimit} permits / {FixedWindow?.Window.TotalSeconds}s " +
                $"with {SlidingWindow?.SegmentsPerWindow} segments, queue {FixedWindow?.QueueLimit}",

            RateLimiterKind.TokenBucket =>
                $"TokenBucket: {TokenBucket?.TokenBucketRefillRate} refill/sec, capacity {TokenBucket?.TokenBucketCapacity}, queue {TokenBucket?.QueueLimit}",

            _ => $"Unknown limiter kind: {Kind}"
        };
    }
}