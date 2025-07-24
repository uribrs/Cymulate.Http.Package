using Microsoft.Extensions.Logging;
using Session.Contracts.Interfaces;
using Session.Contracts.Models.Lifecycle;
using Session.Contracts.Models.Wiring;
using Session.Logic.Wiring;
using Session.Telemetry;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Session.Logic.Lifecycle
{
    public sealed class SessionManager : IAsyncDisposable, IDisposable
    {
        private readonly ConcurrentDictionary<SessionId, IHttpSession> _sessions = new();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<SessionManager> _logger;
        private readonly Timer _cleanupTimer;
        private readonly object _lockObject = new();
        private bool _disposed = false;

        public SessionManager(ILoggerFactory loggerFactory, TimeSpan? cleanupInterval = null)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<SessionManager>();

            // Default cleanup interval is 5 minutes
            var interval = cleanupInterval ?? TimeSpan.FromMinutes(5);
            _cleanupTimer = new Timer(CleanupDisposedSessions, null, interval, interval);

            _logger.LogInformation("[SessionManager] Started with cleanup interval: {Interval}", interval);
        }

        public IHttpSession Create(HttpClientProfile profile, HttpClient client, CancellationToken cancellationToken = default)
        {
            var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.CreateSession);

            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SessionManager));

                var session = SessionBuilder.Build(profile, client, _loggerFactory, cancellationToken);
                var authType = SessionTelemetryConstants.GetAuthType(profile.Auth);

                SessionDiagnostics.RecordProfileInfo(activity, profile.Name, authType);
                SessionDiagnostics.RecordSessionInfo(activity, session.SessionId, session.CreatedAt, session.LastUsed);

                if (session is HttpSession httpSession)
                    httpSession.SessionDisposed += OnSessionDisposed;

                if (!_sessions.TryAdd(session.SessionId, session))
                {
                    if (session is HttpSession httpSession2)
                        httpSession2.SessionDisposed -= OnSessionDisposed;

                    SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Duplicate);
                    throw new InvalidOperationException($"Session ID {session.SessionId} already exists. This indicates a critical error in session ID generation.");
                }

                SessionMetrics.RecordSessionCreated(profile.Name, authType);
                SessionMetrics.UpdateActiveSessionsCount(_sessions.Count);

                SessionDiagnostics.RecordSessionCount(activity, _sessions.Count);
                SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);

                _logger.LogInformation("[SessionManager] Session created: {SessionId}", session.SessionId);
                return session;
            }
            catch (Exception ex)
            {
                SessionDiagnostics.RecordFailure(activity, ex);
                SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        public IHttpSession? Get(SessionId sessionId)
        {
            var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.GetSession);

            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SessionManager));

                var found = _sessions.TryGetValue(sessionId, out var session);

                if (found && session != null)
                {
                    // Update LastUsed timestamp when session is accessed
                    session.UpdateLastUsed();
                    SessionDiagnostics.RecordSessionInfo(activity, session.SessionId, session.CreatedAt, session.LastUsed);
                    SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);

                    // Record metrics for last used updates
                    SessionMetrics.SessionLastUsedUpdates.Add(1);
                }
                else
                {
                    SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.NotFound);
                }

                SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionId, sessionId.ToString());

                _logger.LogDebug("[SessionManager] Get session {SessionId}: {Result}", sessionId, found ? "found" : "not found");
                return session;
            }
            catch (Exception ex)
            {
                SessionDiagnostics.RecordFailure(activity, ex);
                SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        public bool Remove(SessionId sessionId)
        {
            var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.RemoveSession);

            try
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SessionManager));

                var removed = _sessions.TryRemove(sessionId, out var session);

                // Unsubscribe from disposal event if it's an HttpSession
                if (session is HttpSession httpSession)
                {
                    httpSession.SessionDisposed -= OnSessionDisposed;
                    SessionDiagnostics.RecordSessionInfo(activity, session.SessionId, session.CreatedAt, session.LastUsed);

                    // Record metrics for manual removal
                    SessionMetrics.SessionsRemoved.Add(1);
                }

                SessionMetrics.UpdateActiveSessionsCount(_sessions.Count);

                SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionId, sessionId.ToString());
                SessionDiagnostics.RecordSessionCount(activity, _sessions.Count);
                SessionDiagnostics.RecordOutcome(activity, removed ? SessionTelemetryConstants.Outcomes.Success : SessionTelemetryConstants.Outcomes.NotFound);

                _logger.LogInformation("[SessionManager] Removed session {SessionId}: {Result}", sessionId, removed ? "success" : "not found");
                return removed;
            }
            catch (Exception ex)
            {
                SessionDiagnostics.RecordFailure(activity, ex);
                SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        public IReadOnlyCollection<IHttpSession> All
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SessionManager));

                _logger.LogDebug("[SessionManager] Listing all active sessions. Count: {Count}", _sessions.Count);
                return _sessions.Values.ToList();
            }
        }

        /// <summary>
        /// Gets sessions sorted by last used time (oldest first) for LRU or idle-based cleanup
        /// </summary>
        /// <returns>Sessions ordered by LastUsed timestamp (ascending)</returns>
        public IReadOnlyCollection<IHttpSession> GetSessionsByLastUsed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SessionManager));

            var sortedSessions = _sessions.Values
                .OrderBy(session => session.LastUsed)
                .ToList();

            _logger.LogDebug("[SessionManager] Retrieved {Count} sessions sorted by last used time", sortedSessions.Count);
            return sortedSessions;
        }

        /// <summary>
        /// Gets sessions that haven't been used since the specified threshold
        /// </summary>
        /// <param name="threshold">Sessions not used since this time will be returned</param>
        /// <returns>Sessions older than the threshold</returns>
        public IReadOnlyCollection<IHttpSession> GetIdleSessions(DateTimeOffset threshold)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SessionManager));

            var idleSessions = _sessions.Values
                .Where(session => session.LastUsed < threshold)
                .ToList();

            _logger.LogDebug("[SessionManager] Found {Count} idle sessions older than {Threshold}", idleSessions.Count, threshold);
            return idleSessions;
        }

        public void CleanupDisposedSessions()
        {
            var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.CleanupSessions);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (_disposed)
                    return;

                var disposedSessions = new List<SessionId>();

                foreach (var kvp in _sessions)
                {
                    if (kvp.Value.IsDisposed)
                    {
                        disposedSessions.Add(kvp.Key);
                    }
                }

                foreach (var sessionId in disposedSessions)
                {
                    if (_sessions.TryRemove(sessionId, out var session))
                    {
                        // Unsubscribe from disposal event if it's an HttpSession
                        if (session is HttpSession httpSession)
                        {
                            httpSession.SessionDisposed -= OnSessionDisposed;
                        }

                        _logger.LogInformation("[SessionManager] Cleaned up disposed session: {SessionId}", sessionId);
                    }
                }

                stopwatch.Stop();

                // Record metrics for cleanup operation
                SessionMetrics.RecordCleanupOperation(disposedSessions.Count, stopwatch.Elapsed.TotalSeconds);
                SessionMetrics.UpdateActiveSessionsCount(_sessions.Count);

                SessionDiagnostics.RecordCleanupStats(activity, disposedSessions.Count, stopwatch.Elapsed);
                SessionDiagnostics.RecordSessionCount(activity, _sessions.Count);
                SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);

                if (disposedSessions.Count > 0)
                {
                    _logger.LogInformation("[SessionManager] Cleanup completed. Removed {Count} disposed sessions", disposedSessions.Count);
                }

                // Future enhancement: Could also cleanup idle sessions here using GetIdleSessions()
                // Example: var idleSessions = GetIdleSessions(DateTimeOffset.UtcNow.AddMinutes(-30));
            }
            catch (Exception ex)
            {
                SessionDiagnostics.RecordFailure(activity, ex);
                SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private void CleanupDisposedSessions(object? state)
        {
            try
            {
                CleanupDisposedSessions();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SessionManager] Error during automatic cleanup");
            }
        }

        private void OnSessionDisposed(object? sender, SessionId sessionId)
        {
            // Immediate cleanup when a session is disposed
            if (_sessions.TryRemove(sessionId, out var session))
            {
                if (session is HttpSession httpSession)
                {
                    httpSession.SessionDisposed -= OnSessionDisposed;
                }

                SessionMetrics.UpdateActiveSessionsCount(_sessions.Count);

                _logger.LogInformation("[SessionManager] Session automatically cleaned up on disposal: {SessionId}", sessionId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            _logger.LogInformation("[SessionManager] Disposing SessionManager");

            _cleanupTimer?.Dispose();

            // Properly dispose all active sessions asynchronously
            foreach (var kvp in _sessions)
            {
                try
                {
                    await kvp.Value.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SessionManager] Error disposing session {SessionId}", kvp.Key);
                }
            }

            _sessions.Clear();
            _logger.LogInformation("[SessionManager] SessionManager disposed");
        }

        public void Dispose()
        {
            // For synchronous disposal, we need to handle the async disposal synchronously
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
