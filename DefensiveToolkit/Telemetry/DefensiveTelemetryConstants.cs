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