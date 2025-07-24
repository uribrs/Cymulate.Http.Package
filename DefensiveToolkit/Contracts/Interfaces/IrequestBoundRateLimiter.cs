using System.Threading.RateLimiting;

namespace DefensiveToolkit.Contracts.Interfaces;

public interface IrequestBoundRateLimiter
{
    bool ShouldOverrideRateLimiter(HttpRequestMessage request);
    RateLimiter GetCustomLimiter(HttpRequestMessage request);
}