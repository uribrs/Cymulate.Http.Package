using Session.Contracts.Models.Lifecycle;
using System.Diagnostics;

namespace Session.Telemetry
{
    public static class SessionDiagnostics
    {
        public static readonly ActivitySource Source =
            new(SessionTelemetryConstants.ActivitySourceName);

        public static Activity? StartSessionActivity(string operationName)
        {
            var activity = Source.StartActivity($"Session.{operationName}", ActivityKind.Internal);
            return activity;
        }

        public static void SetTag(Activity? activity, string key, object? value)
        {
            if (activity?.IsAllDataRequested == true)
                activity.SetTag(key, value);
        }

        public static void RecordFailure(Activity? activity, Exception ex)
        {
            if (activity is null) return;

            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag(SessionTelemetryConstants.Tags.ExceptionType, ex.GetType().FullName);
            activity.SetTag(SessionTelemetryConstants.Tags.ExceptionMessage, ex.Message);
            activity.SetTag(SessionTelemetryConstants.Tags.ExceptionStackTrace, ex.StackTrace);
        }

        public static void RecordOutcome(Activity? activity, string outcome)
        {
            if (activity?.IsAllDataRequested == true)
                activity.SetTag(SessionTelemetryConstants.Tags.Outcome, outcome);
        }

        public static void RecordSessionInfo(Activity? activity, SessionId sessionId, DateTimeOffset createdAt, DateTimeOffset lastUsed)
        {
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag(SessionTelemetryConstants.Tags.SessionId, sessionId.ToString());
                activity.SetTag(SessionTelemetryConstants.Tags.SessionCreatedAt, createdAt.ToString("O"));
                activity.SetTag(SessionTelemetryConstants.Tags.SessionLastUsed, lastUsed.ToString("O"));
                activity.SetTag(SessionTelemetryConstants.Tags.SessionLifetime,
                    (DateTimeOffset.UtcNow - createdAt).TotalMilliseconds.ToString());
            }
        }

        public static void RecordSessionCount(Activity? activity, int count)
        {
            if (activity?.IsAllDataRequested == true)
                activity.SetTag(SessionTelemetryConstants.Tags.SessionsCount, count.ToString());
        }

        public static void RecordCleanupStats(Activity? activity, int cleanedUp, TimeSpan duration)
        {
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag(SessionTelemetryConstants.Tags.SessionsCleanedUp, cleanedUp.ToString());
                activity.SetTag(SessionTelemetryConstants.Tags.CleanupDuration, duration.TotalMilliseconds.ToString());
            }
        }

        public static void RecordProfileInfo(Activity? activity, string profileName, string authType)
        {
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag(SessionTelemetryConstants.Tags.ProfileName, profileName);
                activity.SetTag(SessionTelemetryConstants.Tags.AuthType, authType);
            }
        }

        public static void RecordRefreshInfo(Activity? activity, int attempts, bool success, string reason)
        {
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag(SessionTelemetryConstants.Tags.RefreshAttempts, attempts.ToString());
                activity.SetTag(SessionTelemetryConstants.Tags.RefreshSuccess, success.ToString());
                activity.SetTag(SessionTelemetryConstants.Tags.RefreshReason, reason);
            }
        }
    }
}
