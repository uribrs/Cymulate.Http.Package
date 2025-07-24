using Authentication.Contracts.Models;

namespace Session.Telemetry;

public static class SessionTelemetryConstants
{
    public const string ActivitySourceName = "PowerHttp.Session";

    public static class Tags
    {
        public const string SessionId = "session.id";
        public const string SessionCreatedAt = "session.created_at";
        public const string SessionLastUsed = "session.last_used";
        public const string SessionLifetime = "session.lifetime_ms";
        public const string SessionAutoRefresh = "session.auto_refresh";
        public const string SessionsCount = "sessions.count";
        public const string SessionsCleanedUp = "sessions.cleaned_up";
        public const string CleanupDuration = "cleanup.duration_ms";
        public const string ProfileName = "profile.name";
        public const string AuthType = "auth.type";
        public const string RefreshAttempts = "refresh.attempts";
        public const string RefreshSuccess = "refresh.success";
        public const string RefreshReason = "refresh.reason";

        public const string ResponseStatusCode = "response.status_code";

        // Reuse common tags from DefensiveToolkit
        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStackTrace = "exception.stacktrace";
        public const string Outcome = "outcome";
    }

    public static class Operations
    {
        public const string CreateSession = "session.create";
        public const string GetSession = "session.get";
        public const string RemoveSession = "session.remove";
        public const string CleanupSessions = "session.cleanup";
        public const string RefreshToken = "session.refresh_token";
        public const string UpdateLastUsed = "session.update_last_used";
        public const string DisposeSession = "session.dispose";
        public const string ExecuteRequest = "session.execute_request";
    }

    public static class Outcomes
    {
        public const string Success = "Success";
        public const string Failure = "Failure";
        public const string NotFound = "NotFound";
        public const string Duplicate = "Duplicate";
        public const string Disposed = "Disposed";
        public const string Refreshed = "Refreshed";
        public const string Expired = "Expired";
        public const string Cancelled = "Cancelled";
    }

    public static class AuthTypes
    {
        public const string NoAuth = "NoAuth";
        public const string ApiKey = "ApiKey";
        public const string Basic = "Basic";
        public const string Bearer = "Bearer";
        public const string Jwt = "Jwt";
        public const string OAuth2 = "OAuth2";
        public const string Unknown = "Unknown";
    }

    /// <summary>
    /// Helper method to extract a more specific auth type from AuthSelection
    /// </summary>
    public static string GetAuthType(AuthSelection authSelection)
    {
        return authSelection switch
        {
            AuthSelection.None => AuthTypes.NoAuth,
            AuthSelection.ApiKey => AuthTypes.ApiKey,
            AuthSelection.Basic => AuthTypes.Basic,
            AuthSelection.Bearer => AuthTypes.Bearer,
            AuthSelection.Jwt => AuthTypes.Jwt,
            AuthSelection.OAuth2 => AuthTypes.OAuth2,
            _ => AuthTypes.Unknown
        };
    }
}