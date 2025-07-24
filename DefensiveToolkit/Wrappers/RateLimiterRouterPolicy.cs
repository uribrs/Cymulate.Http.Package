using DefensiveToolkit.Contracts;
using DefensiveToolkit.Core;
using DefensiveToolkit.Telemetry;

namespace DefensiveToolkit.Wrappers;

public class RateLimiterRouterPolicy : IResiliencePolicy
{
    private readonly IResiliencePolicy _globalPolicy;
    private readonly Func<HttpRequestMessage?, IResiliencePolicy?> _requestOverrideResolver;

    public RateLimiterRouterPolicy(
        IResiliencePolicy globalPolicy,
        Func<HttpRequestMessage?, IResiliencePolicy?> requestOverrideResolver)
    {
        _globalPolicy = globalPolicy ?? throw new ArgumentNullException(nameof(globalPolicy));
        _requestOverrideResolver = requestOverrideResolver ?? throw new ArgumentNullException(nameof(requestOverrideResolver));
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var request = RequestContextScope.Current;
        var resolved = _requestOverrideResolver(request) ?? _globalPolicy;

        using var activity = DefensiveDiagnostics.StartPolicyActivity("RateLimiterRouterPolicy", "ExecuteAsync");
        DefensiveDiagnostics.SetTag(activity, "rate_limiter.source", resolved == _globalPolicy ? "global" : "override");

        return await resolved.ExecuteAsync(operation, cancellationToken);
    }
}