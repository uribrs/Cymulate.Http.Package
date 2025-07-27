using System.Threading.RateLimiting;

namespace DefensiveToolkit.Contracts.Options.RateLimiterKindOptions;

public sealed class FixedWindowOptions 
{
    public int PermitLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
    public int QueueLimit { get; set; } = 0; // 0 = reject when full
    public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;
    public bool AutoReplenishment { get; set; } = true;
}