namespace DefensiveToolkit.Contracts.Options.RateLimiterKindOptions;

public sealed class SlidingWindowOptions 
{
    public int SegmentsPerWindow { get; set; } = 1;
}