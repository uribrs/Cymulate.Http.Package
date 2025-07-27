using System.Threading.RateLimiting;

namespace DefensiveToolkit.Contracts.Options.RateLimiterKindOptions;

public sealed class TokenBucketOptions 
{
    public double TokenBucketRefillRate { get; set; } = 1.0;     // tokens per second
    public int TokenBucketCapacity { get; set; } = 10;
    public int QueueLimit { get; set; } = 0; // 0 = reject when full
    public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;
    public bool AutoReplenishment { get; set; } = true;
}