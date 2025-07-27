namespace DefensiveToolkit.Contracts.Options.RateLimiterKindOptions;

public sealed class FixedWindowOptions 
{
    public int PermitLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
    public int QueueLimit { get; set; } = 0; // 0 = reject when full
}