using DefensiveToolkit.Policies;

namespace Session.Contracts.Models.Lifecycle;

public static class RequestOptionKeys
{
    public static readonly HttpRequestOptionsKey<RateLimiterPolicy> CustomRateLimiterKey =
        new("X-CustomRateLimiter");
}