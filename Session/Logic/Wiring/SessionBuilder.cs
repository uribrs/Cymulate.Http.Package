using Authentication.Logic.Services;
using DefensiveToolkit.Core;
using DefensiveToolkit.Policies;
using DefensiveToolkit.Wrappers;
using Microsoft.Extensions.Logging;
using Session.Contracts.Models.Lifecycle;
using Session.Contracts.Models.Wiring;
using Session.Logic.Lifecycle;

namespace Session.Logic.Wiring;

public static class SessionBuilder
{
    public static HttpSession Build(
        HttpClientProfile profile,
        HttpClient client,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        cancellationToken.ThrowIfCancellationRequested();

        var authProvider = AuthenticationOrchestrator.Create(profile.Auth, loggerFactory);

        var timeoutPolicy = new TimeoutPolicy(profile.Timeout);

        var retryPolicy = profile.Retry is not null
            ? new RetryPolicy(profile.Retry)
            : RetryPolicy.NoOp;

        var globalRateLimiter = profile.RateLimiter is not null
            ? new RateLimiterPolicy(profile.RateLimiter)
            : RateLimiterPolicy.NoOp;

        RateLimiterPolicy? CustomResolver(HttpRequestMessage? request)
        {
            if (request?.Options.TryGetValue(RequestOptionKeys.CustomRateLimiterKey, out var overrideLimiter) == true)
                return overrideLimiter;

            return null;
        }

        var rateLimiter = new RateLimiterRouterPolicy(globalRateLimiter, CustomResolver);

        var circuitBreaker = profile.CircuitBreaker is not null
            ? new CircuitBreakerPolicy(profile.CircuitBreaker)
            : CircuitBreakerPolicy.NoOp;

        var runner = new DefensivePolicyRunner(
            rateLimiter,
            timeoutPolicy,
        retryPolicy,
            circuitBreaker
        );

        var sessionLogger = loggerFactory.CreateLogger<HttpSession>();
        cancellationToken.ThrowIfCancellationRequested();

        return new HttpSession(profile, client, authProvider, runner, sessionLogger, cancellationToken);
    }
}