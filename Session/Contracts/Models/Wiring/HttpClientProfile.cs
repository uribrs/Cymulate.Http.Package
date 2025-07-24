using Authentication.Contracts.Models;
using DefensiveToolkit.Contracts.Options;

namespace Session.Contracts.Models.Wiring;

public sealed class HttpClientProfile
{
    public required string Name { get; init; }

    // 🔐 Auth
    public required AuthSelection Auth { get; init; }

    // ⏱ Timeout
    public TimeoutOptions Timeout { get; init; } = new();

    // 🔁 Retry
    public RetryOptions? Retry { get; init; }

    // 🚦 Rate Limiter
    public RateLimiterOptions? RateLimiter { get; init; }

    // 💥 Circuit Breaker
    public CircuitBreakerOptions? CircuitBreaker { get; init; }

    public HttpClientSecurityOptions Security { get; init; } = new();

    // 🧩 Future: Headers, BaseUrl, EnableHttp2, etc.
}