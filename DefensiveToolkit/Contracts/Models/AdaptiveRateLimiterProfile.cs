using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Contracts.Options.RateLimiterKindOptions;

namespace DefensiveToolkit.Contracts.Models;

public sealed class AdaptiveRateLimiterProfile
{
    public FixedWindowOptions? FixedWindow { get; init; }
    public SlidingWindowOptions? SlidingWindow { get; init; }
    public TokenBucketOptions? TokenBucket { get; init; }
    public MachineCategory Category { get; init; }
}