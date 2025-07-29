using DefensiveToolkit.Contracts.Interfaces;
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
        
        // Track rate limiter selection details
        DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSource, resolved == _globalPolicy ? "global" : "override");
        DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterPolicyType, resolved.GetType().Name);
        DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterHasRequestContext, request is not null);
        
        if (request is not null)
        {
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterRequestUri, request.RequestUri?.ToString());
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterRequestMethod, request.Method.ToString());
        }

        return await resolved.ExecuteAsync(operation, cancellationToken);
    }
}