using DefensiveToolkit.Contracts.Enums;

namespace DefensiveToolkit.Telemetry;

public static class DefensiveTelemetryConstants
{
    public const string ActivitySourceName = "PowerHttp.DefensiveToolkit";

    public static class Tags
    {
        public const string Policy = "policy.name";
        public const string Outcome = "policy.outcome";
        public const string RetryCount = "retry.attempts";
        public const string TimeoutThreshold = "timeout.threshold_ms";
        public const string CircuitBreakerState = "circuit.state";
        public const string RateLimiterWait = "rate_limiter.wait_duration_ms";

        // Rate Limiter Configuration Tags
        public const string RateLimiterKind = "rate_limiter.kind";
        public const string RateLimiterSource = "rate_limiter.source";
        public const string RateLimiterPolicyType = "rate_limiter.policy_type";
        public const string RateLimiterHasRequestContext = "rate_limiter.has_request_context";
        public const string RateLimiterRequestUri = "rate_limiter.request_uri";
        public const string RateLimiterRequestMethod = "rate_limiter.request_method";

        // Machine Profile Tags
        public const string MachineCpuCores = "machine.cpu_cores";
        public const string MachineRamGb = "machine.ram_gb";
        public const string MachineCategory = "machine.category";

        // Rate Limiter Profile Tags
        public const string RateLimiterProfileCategory = "rate_limiter.profile.category";
        public const string RateLimiterProfileFixedWindowConfigured = "rate_limiter.profile.fixed_window_configured";
        public const string RateLimiterProfileSlidingWindowConfigured = "rate_limiter.profile.sliding_window_configured";
        public const string RateLimiterProfileTokenBucketConfigured = "rate_limiter.profile.token_bucket_configured";

        // Rate Limiter Configuration Tags
        public const string RateLimiterFixedWindowPermitLimit = "rate_limiter.fixed_window.permit_limit";
        public const string RateLimiterFixedWindowWindowSeconds = "rate_limiter.fixed_window.window_seconds";
        public const string RateLimiterFixedWindowQueueLimit = "rate_limiter.fixed_window.queue_limit";
        public const string RateLimiterSlidingWindowPermitLimit = "rate_limiter.sliding_window.permit_limit";
        public const string RateLimiterSlidingWindowWindowSeconds = "rate_limiter.sliding_window.window_seconds";
        public const string RateLimiterSlidingWindowSegments = "rate_limiter.sliding_window.segments";
        public const string RateLimiterSlidingWindowQueueLimit = "rate_limiter.sliding_window.queue_limit";
        public const string RateLimiterTokenBucketCapacity = "rate_limiter.token_bucket.capacity";
        public const string RateLimiterTokenBucketRefillRate = "rate_limiter.token_bucket.refill_rate";
        public const string RateLimiterTokenBucketQueueLimit = "rate_limiter.token_bucket.queue_limit";

        // Adaptive Rate Limiter Tags
        public const string RateLimiterAdaptiveCategory = "rate_limiter.adaptive.category";
        public const string RateLimiterAdaptiveApplied = "rate_limiter.adaptive.applied";
        public const string RateLimiterAdaptiveAppliedCount = "rate_limiter.adaptive.applied_count";

        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStackTrace = "exception.stacktrace";
    }

    public static class Outcomes
    {
        public const string Success = nameof(OutcomesEnum.Success);
        public const string Failure = nameof(OutcomesEnum.Failure);
        public const string Cancelled = nameof(OutcomesEnum.Cancelled);
        public const string Rejected = nameof(OutcomesEnum.Rejected);
    }
}