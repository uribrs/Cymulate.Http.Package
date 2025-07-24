using System.Diagnostics.Metrics;

namespace Session.Telemetry;

internal static class SessionMetrics
{
    private static readonly Meter Meter = new("PowerHttp.Session");

    // Counters for event tracking
    public static readonly Counter<long> SessionsCreated = Meter.CreateCounter<long>(
        "session.created",
        description: "Number of sessions created");

    public static readonly Counter<long> SessionsDisposed = Meter.CreateCounter<long>(
        "session.disposed",
        description: "Number of sessions disposed");

    public static readonly Counter<long> SessionsRemoved = Meter.CreateCounter<long>(
        "session.removed",
        description: "Number of sessions manually removed");

    public static readonly Counter<long> SessionsCleanedUp = Meter.CreateCounter<long>(
        "session.cleaned_up",
        description: "Number of sessions cleaned up during cleanup operations");

    public static readonly Counter<long> SessionRefreshAttempts = Meter.CreateCounter<long>(
        "session.refresh_attempts",
        description: "Number of session refresh attempts");

    public static readonly Counter<long> SessionRefreshFailures = Meter.CreateCounter<long>(
        "session.refresh_failures",
        description: "Number of session refresh failures");

    public static readonly Counter<long> SessionLastUsedUpdates = Meter.CreateCounter<long>(
        "session.last_used_updates",
        description: "Number of times session last used timestamp was updated");

    // Histograms for duration and distribution tracking
    public static readonly Histogram<double> SessionLifetime = Meter.CreateHistogram<double>(
        "session.lifetime_seconds",
        unit: "s",
        description: "Session lifetime in seconds");

    public static readonly Histogram<double> CleanupDuration = Meter.CreateHistogram<double>(
        "session.cleanup_duration_seconds",
        unit: "s",
        description: "Time taken to perform session cleanup operations");

    public static readonly Histogram<double> RefreshBackoff = Meter.CreateHistogram<double>(
        "session.refresh_backoff_seconds",
        unit: "s",
        description: "Time between refresh attempts in seconds");

    // Gauges for current state tracking
    public static readonly ObservableGauge<int> ActiveSessions = Meter.CreateObservableGauge<int>(
        "session.active_count",
        () => _activeSessionCount,
        description: "Current number of active sessions");

    private static int _activeSessionCount = 0;

    // Helper method to record session creation with tags
    public static void RecordSessionCreated(string profileName, string authType)
    {
        SessionsCreated.Add(1,
            new KeyValuePair<string, object?>("profile.name", profileName),
            new KeyValuePair<string, object?>("auth.type", authType));
    }

    // Helper method to record session disposal with tags
    public static void RecordSessionDisposed(double lifetimeSeconds, string authType, int refreshAttempts)
    {
        SessionsDisposed.Add(1,
            new KeyValuePair<string, object?>("auth.type", authType),
            new KeyValuePair<string, object?>("refresh.attempts", refreshAttempts));

        SessionLifetime.Record(lifetimeSeconds,
            new KeyValuePair<string, object?>("auth.type", authType));
    }

    // Helper method to record cleanup operations
    public static void RecordCleanupOperation(int cleanedUpCount, double durationSeconds)
    {
        SessionsCleanedUp.Add(cleanedUpCount);
        CleanupDuration.Record(durationSeconds);
    }

    // Helper method to record refresh attempts
    public static void RecordRefreshAttempt(bool success, string reason, string authType)
    {
        SessionRefreshAttempts.Add(1,
            new KeyValuePair<string, object?>("auth.type", authType),
            new KeyValuePair<string, object?>("reason", reason));

        if (!success)
        {
            SessionRefreshFailures.Add(1,
                new KeyValuePair<string, object?>("auth.type", authType),
                new KeyValuePair<string, object?>("reason", reason));
        }
    }

    // Helper method to set active sessions count
    public static void UpdateActiveSessionsCount(int count)
    {
        _activeSessionCount = count;
    }
}