using Authentication.Logic.Services;
using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Core;
using DefensiveToolkit.MachineProfileAdapter;
using DefensiveToolkit.Policies;
using DefensiveToolkit.Wrappers;
using Microsoft.Extensions.Logging;
using Session.Contracts.Models.Lifecycle;
using Session.Contracts.Models.Wiring;
using Session.Logic.Lifecycle;
using Session.Telemetry;

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

        var authProvider = AuthenticationOrchestrator.Create(profile.Auth, loggerFactory, client);

        var timeoutPolicy = new TimeoutPolicy(profile.Timeout);

        var retryPolicy = profile.Retry is not null
            ? new RetryPolicy(profile.Retry)
            : RetryPolicy.NoOp;

        ApplyAdaptiveRateLimiterFallback(profile, loggerFactory);

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

    private static void ApplyAdaptiveRateLimiterFallback(HttpClientProfile profile, ILoggerFactory loggerFactory)
    {
        if (profile.RateLimiter is null)
            return;

        var activity = SessionDiagnostics.StartSessionActivity("ApplyAdaptiveRateLimiter");
        var logger = loggerFactory.CreateLogger("RateLimiterAdaptor");

        try
        {
            var adaptive = RateLimiterAdaptor.GetProfile(logger);
            
            // Track the adaptive profile selection
            SessionDiagnostics.SetTag(activity, "rate_limiter.adaptive.category", adaptive.Category.ToString());
            SessionDiagnostics.SetTag(activity, "rate_limiter.kind", profile.RateLimiter.Kind.ToString());
            SessionDiagnostics.SetTag(activity, "rate_limiter.adaptive.applied", false);

            var appliedCount = 0;

            switch (profile.RateLimiter.Kind)
            {
                case RateLimiterKind.FixedWindow:
                    if (profile.RateLimiter.FixedWindow is null && adaptive.FixedWindow is not null)
                    {
                        profile.RateLimiter.FixedWindow = adaptive.FixedWindow;
                        appliedCount++;
                        
                        // Track the applied configuration
                        SessionDiagnostics.SetTag(activity, "rate_limiter.fixed_window.permit_limit", adaptive.FixedWindow.PermitLimit);
                        SessionDiagnostics.SetTag(activity, "rate_limiter.fixed_window.window_seconds", adaptive.FixedWindow.Window.TotalSeconds);
                        SessionDiagnostics.SetTag(activity, "rate_limiter.fixed_window.queue_limit", adaptive.FixedWindow.QueueLimit);
                    }
                    break;

                case RateLimiterKind.SlidingWindow:
                    if (profile.RateLimiter.SlidingWindow is null && adaptive.SlidingWindow is not null)
                    {
                        profile.RateLimiter.SlidingWindow = adaptive.SlidingWindow;
                        appliedCount++;
                        
                        // Track the applied configuration
                        SessionDiagnostics.SetTag(activity, "rate_limiter.sliding_window.permit_limit", adaptive.SlidingWindow.PermitLimit);
                        SessionDiagnostics.SetTag(activity, "rate_limiter.sliding_window.window_seconds", adaptive.SlidingWindow.Window.TotalSeconds);
                        SessionDiagnostics.SetTag(activity, "rate_limiter.sliding_window.segments", adaptive.SlidingWindow.SegmentsPerWindow);
                        SessionDiagnostics.SetTag(activity, "rate_limiter.sliding_window.queue_limit", adaptive.SlidingWindow.QueueLimit);
                    }
                    break;

                case RateLimiterKind.TokenBucket:
                    if (profile.RateLimiter.TokenBucket is null && adaptive.TokenBucket is not null)
                    {
                        profile.RateLimiter.TokenBucket = adaptive.TokenBucket;
                        appliedCount++;
                        
                        // Track the applied configuration
                        SessionDiagnostics.SetTag(activity, "rate_limiter.token_bucket.capacity", adaptive.TokenBucket.TokenBucketCapacity);
                        SessionDiagnostics.SetTag(activity, "rate_limiter.token_bucket.refill_rate", adaptive.TokenBucket.TokenBucketRefillRate);
                        SessionDiagnostics.SetTag(activity, "rate_limiter.token_bucket.queue_limit", adaptive.TokenBucket.QueueLimit);
                    }
                    break;
            }

            SessionDiagnostics.SetTag(activity, "rate_limiter.adaptive.applied", appliedCount > 0);
            SessionDiagnostics.SetTag(activity, "rate_limiter.adaptive.applied_count", appliedCount);
            
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
            
            logger.LogInformation("[SessionBuilder] Applied {AppliedCount} adaptive rate limiter settings for {Kind} limiter", 
                appliedCount, profile.RateLimiter.Kind);
        }
        catch (Exception ex)
        {
            SessionDiagnostics.RecordFailure(activity, ex);
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
            logger.LogError(ex, "[SessionBuilder] Failed to apply adaptive rate limiter fallback");
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }
}
