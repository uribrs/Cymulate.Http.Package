using DefensiveToolkit.Contracts.Enums;

namespace DefensiveToolkit.Contracts.Options;

public class RateLimiterOptions
{
    public int PermitLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
    public int QueueLimit { get; set; } = 0; // 0 = reject when full
    public RateLimiterKind Kind { get; set; } = RateLimiterKind.FixedWindow;
    public double TokenBucketRefillRate { get; set; } = 1.0;     // tokens per second
    public int TokenBucketCapacity { get; set; } = 10;
    public int SegmentsPerWindow { get; set; } = 1;              // for SlidingWindow granularity
}