# Session Core - Lifecycle Management

This document covers the core session lifecycle components: `HttpSession`, `SessionManager`, and `SessionBuilder`. These components handle session creation, management, and disposal.

## Table of Contents

- [Overview](#overview)
- [HttpSession](#httpsession)
- [SessionManager](#sessionmanager)
- [SessionBuilder](#sessionbuilder)
- [Session Lifecycle](#session-lifecycle)
- [Resource Management](#resource-management)
- [Thread Safety](#thread-safety)
- [Error Handling](#error-handling)

## Overview

The core session components provide:

- **Session Creation**: Factory pattern for creating properly configured sessions
- **Lifecycle Management**: Tracking session state and usage patterns
- **Resource Cleanup**: Automatic disposal of sessions and associated resources
- **Session Orchestration**: Managing multiple sessions with cleanup and lookup capabilities

## HttpSession

The `HttpSession` class is the main implementation of `IHttpSession` that encapsulates HTTP operations with authentication and resilience policies.

### Key Properties

```csharp
public sealed class HttpSession : IHttpSession, IAsyncDisposable
{
    public SessionId SessionId { get; }                    // Unique session identifier
    public DateTimeOffset CreatedAt { get; }               // Session creation timestamp
    public DateTimeOffset LastUsed { get; private set; }   // Last usage timestamp
    public bool IsDisposed { get; private set; }           // Disposal state
    public bool AutoRefreshEnabled { get; }                // Token refresh status
    
    public IAuthenticationProvider Auth { get; }           // Authentication provider
    public IDefensivePolicyRunner Policies { get; }        // Resilience policies
}
```

### Session Creation

```csharp
var session = new HttpSession(
    profile,           // HttpClientProfile with configuration
    httpClient,        // HttpClient instance
    authProvider,      // IAuthenticationProvider
    policies,          // IDefensivePolicyRunner
    logger,            // ILogger<HttpSession>
    cancellationToken  // Global cancellation token
);
```

### Automatic Token Refresh

When using refreshable authenticators (OAuth2, JWT), the session automatically starts a background refresh loop:

```csharp
// Background refresh loop starts automatically for OAuth2/JWT
if (auth is IRefreshableAuthenticator refreshable && refreshable.ExpiresAt > DateTimeOffset.UtcNow)
{
    // Starts background task that refreshes tokens before expiry
    _refreshLoop = StartRefreshLoop(refreshable, _refreshCts.Token);
}
```

### Refresh Loop Behavior

```csharp
private Task StartRefreshLoop(IRefreshableAuthenticator refreshable, CancellationToken token)
{
    return Task.Run(async () =>
    {
        while (!token.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var expiresAt = refreshable.ExpiresAt;
            var timeLeft = expiresAt - now;

            // Refresh if token expires within 1 minute
            if (timeLeft <= TimeSpan.FromMinutes(1))
            {
                await refreshable.RefreshAsync(token);
                _logger?.LogInformation("Token refreshed. New expiry: {ExpiresAt}", refreshable.ExpiresAt);
            }

            // Wait for half the remaining time before next check
            var waitTime = TimeSpan.FromSeconds(Math.Max(30, timeLeft.TotalSeconds / 2));
            await Task.Delay(waitTime, token);
        }
    }, token);
}
```

### Usage Tracking

The session automatically tracks usage patterns:

```csharp
public void UpdateLastUsed()
{
    lock (_lastUsedLock)
    {
        LastUsed = DateTimeOffset.UtcNow;
    }
    
    // Called automatically on every request
    SessionDiagnostics.RecordSessionInfo(activity, SessionId, CreatedAt, LastUsed);
}
```

## SessionManager

The `SessionManager` orchestrates multiple sessions, providing lifecycle management and cleanup capabilities.

### Key Features

- **Session Tracking**: Maintains a collection of active sessions
- **Automatic Cleanup**: Removes disposed sessions automatically
- **Lookup Helpers**: Find sessions by ID with LRU updates
- **Idle Session Detection**: Identify sessions that haven't been used recently

### Constructor and Configuration

```csharp
public SessionManager(ILoggerFactory loggerFactory, TimeSpan? cleanupInterval = null)
{
    _loggerFactory = loggerFactory;
    _logger = _loggerFactory.CreateLogger<SessionManager>();
    
    // Default cleanup interval is 5 minutes
    var interval = cleanupInterval ?? TimeSpan.FromMinutes(5);
    _cleanupTimer = new Timer(CleanupDisposedSessions, null, interval, interval);
}
```

### Session Creation

```csharp
public IHttpSession Create(HttpClientProfile profile, HttpClient client, CancellationToken cancellationToken = default)
{
    var session = SessionBuilder.Build(profile, client, _loggerFactory, cancellationToken);
    
    // Subscribe to disposal events
    if (session is HttpSession httpSession)
        httpSession.SessionDisposed += OnSessionDisposed;
    
    // Track the session
    if (!_sessions.TryAdd(session.SessionId, session))
    {
        throw new InvalidOperationException($"Session ID {session.SessionId} already exists.");
    }
    
    SessionMetrics.RecordSessionCreated(profile.Name, authType);
    SessionMetrics.UpdateActiveSessionsCount(_sessions.Count);
    
    return session;
}
```

### Session Lookup

```csharp
public IHttpSession? Get(SessionId sessionId)
{
    var found = _sessions.TryGetValue(sessionId, out var session);
    
    if (found && session != null)
    {
        // Update LastUsed timestamp when session is accessed
        session.UpdateLastUsed();
        SessionMetrics.SessionLastUsedUpdates.Add(1);
    }
    
    return session;
}
```

### Session Removal

```csharp
public bool Remove(SessionId sessionId)
{
    var removed = _sessions.TryRemove(sessionId, out var session);
    
    if (removed && session is HttpSession httpSession)
    {
        httpSession.SessionDisposed -= OnSessionDisposed;
        SessionMetrics.SessionsRemoved.Add(1);
    }
    
    SessionMetrics.UpdateActiveSessionsCount(_sessions.Count);
    return removed;
}
```

### Cleanup Operations

#### Automatic Cleanup

```csharp
public void CleanupDisposedSessions()
{
    var disposedSessions = new List<SessionId>();
    
    // Find disposed sessions
    foreach (var kvp in _sessions)
    {
        if (kvp.Value.IsDisposed)
        {
            disposedSessions.Add(kvp.Key);
        }
    }
    
    // Remove disposed sessions
    foreach (var sessionId in disposedSessions)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            if (session is HttpSession httpSession)
            {
                httpSession.SessionDisposed -= OnSessionDisposed;
            }
        }
    }
    
    SessionMetrics.RecordCleanupOperation(disposedSessions.Count, stopwatch.Elapsed.TotalSeconds);
}
```

#### Manual Cleanup Helpers

```csharp
// Get sessions sorted by last used time (oldest first)
public IReadOnlyCollection<IHttpSession> GetSessionsByLastUsed()
{
    return _sessions.Values
        .OrderBy(session => session.LastUsed)
        .ToList();
}

// Get sessions that haven't been used since the specified threshold
public IReadOnlyCollection<IHttpSession> GetIdleSessions(DateTimeOffset threshold)
{
    return _sessions.Values
        .Where(session => session.LastUsed < threshold)
        .ToList();
}
```

### Disposal Event Handling

```csharp
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
    }
}
```

## SessionBuilder

The `SessionBuilder` is a factory class that creates properly configured `HttpSession` instances with all dependencies wired correctly.

### Factory Method

```csharp
public static HttpSession Build(
    HttpClientProfile profile,
    HttpClient client,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken = default)
{
    // Create authentication provider
    var authProvider = AuthenticationOrchestrator.Create(profile.Auth, loggerFactory, client);
    
    // Create resilience policies
    var timeoutPolicy = new TimeoutPolicy(profile.Timeout);
    var retryPolicy = profile.Retry is not null ? new RetryPolicy(profile.Retry) : RetryPolicy.NoOp;
    var rateLimiter = profile.RateLimiter is not null ? new RateLimiterPolicy(profile.RateLimiter) : RateLimiterPolicy.NoOp;
    var circuitBreaker = profile.CircuitBreaker is not null ? new CircuitBreakerPolicy(profile.CircuitBreaker) : CircuitBreakerPolicy.NoOp;
    
    // Apply adaptive rate limiting fallback
    ApplyAdaptiveRateLimiterFallback(profile, loggerFactory);
    
    // Create policy runner
    var runner = new DefensivePolicyRunner(rateLimiter, timeoutPolicy, retryPolicy, circuitBreaker);
    
    // Create session
    var sessionLogger = loggerFactory.CreateLogger<HttpSession>();
    return new HttpSession(profile, client, authProvider, runner, sessionLogger, cancellationToken);
}
```

### Adaptive Rate Limiting

The builder automatically applies machine-specific rate limiting when not explicitly configured:

```csharp
private static void ApplyAdaptiveRateLimiterFallback(HttpClientProfile profile, ILoggerFactory loggerFactory)
{
    if (profile.RateLimiter is null) return;
    
    var adaptive = RateLimiterAdaptor.GetProfile(logger);
    
    switch (profile.RateLimiter.Kind)
    {
        case RateLimiterKind.FixedWindow:
            if (profile.RateLimiter.FixedWindow is null && adaptive.FixedWindow is not null)
            {
                profile.RateLimiter.FixedWindow = adaptive.FixedWindow;
            }
            break;
            
        case RateLimiterKind.SlidingWindow:
            if (profile.RateLimiter.SlidingWindow is null && adaptive.SlidingWindow is not null)
            {
                profile.RateLimiter.SlidingWindow = adaptive.SlidingWindow;
            }
            break;
            
        case RateLimiterKind.TokenBucket:
            if (profile.RateLimiter.TokenBucket is null && adaptive.TokenBucket is not null)
            {
                profile.RateLimiter.TokenBucket = adaptive.TokenBucket;
            }
            break;
    }
}
```

## Session Lifecycle

### 1. Creation Phase

```csharp
// 1. Profile configuration
var profile = new HttpClientProfile { /* ... */ };

// 2. Session creation via builder
var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// 3. Optional: Add to manager
var mgr = new SessionManager(loggerFactory);
mgr.Create(profile, httpClient);
```

### 2. Usage Phase

```csharp
// Execute requests (updates LastUsed automatically)
var response = await session.ExecuteRequestAsync(request);

// Stream operations
var streamed = await session.StreamResponseAsync(request);

// Token refresh happens automatically in background
```

### 3. Disposal Phase

```csharp
// Manual disposal
await session.DisposeAsync();

// Automatic cleanup by manager
mgr.CleanupDisposedSessions();

// Manager disposal
await mgr.DisposeAsync();
```

## Resource Management

### Disposal Order

1. **Session Disposal**: Stops refresh loops and releases session resources
2. **Manager Cleanup**: Removes disposed sessions from tracking
3. **HttpClient Disposal**: Should be disposed after session disposal

```csharp
// Correct disposal order
await session.DisposeAsync();
httpClient.Dispose();

// Or use using statements
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);
using var httpClient = new HttpClient();
```

### Automatic Cleanup

The `SessionManager` provides automatic cleanup through:

- **Timer-based cleanup**: Removes disposed sessions every 5 minutes (configurable)
- **Event-based cleanup**: Immediate removal when sessions are disposed
- **Manual cleanup**: Helper methods for custom cleanup strategies

## Thread Safety

### HttpSession Thread Safety

- **LastUsed updates**: Protected by `_lastUsedLock`
- **Disposal state**: Thread-safe disposal with `IsDisposed` flag
- **Refresh loop**: Cancellation token-based coordination

### SessionManager Thread Safety

- **Session collection**: Uses `ConcurrentDictionary<SessionId, IHttpSession>`
- **Disposal coordination**: Lock-based disposal state management
- **Timer operations**: Exception handling in timer callbacks

## Error Handling

### Session Creation Errors

```csharp
try
{
    var session = SessionBuilder.Build(profile, httpClient, loggerFactory);
}
catch (ArgumentException ex)
{
    // Invalid profile configuration
}
catch (OperationCanceledException ex)
{
    // Cancellation during creation
}
```

### Session Usage Errors

```csharp
try
{
    var response = await session.ExecuteRequestAsync(request);
}
catch (ObjectDisposedException ex)
{
    // Session was disposed
}
catch (HttpRequestException ex)
{
    // Network errors (handled by retry policies)
}
```

### Manager Errors

```csharp
try
{
    var session = mgr.Create(profile, httpClient);
}
catch (InvalidOperationException ex)
{
    // Duplicate session ID (should not happen)
}
catch (ObjectDisposedException ex)
{
    // Manager was disposed
}
```

### Cleanup Error Handling

```csharp
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
```

## Best Practices

### 1. Session Reuse

```csharp
// Good: Reuse sessions for multiple requests
var session = mgr.Create(profile, httpClient);
for (int i = 0; i < 100; i++)
{
    var response = await session.ExecuteRequestAsync(request);
}

// Avoid: Creating new sessions for each request
for (int i = 0; i < 100; i++)
{
    await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);
    var response = await session.ExecuteRequestAsync(request);
}
```

### 2. Proper Disposal

```csharp
// Good: Use await using for automatic disposal
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// Good: Manual disposal with proper order
await session.DisposeAsync();
httpClient.Dispose();

// Avoid: Disposing HttpClient before session
httpClient.Dispose();
await session.DisposeAsync(); // May cause issues
```

### 3. Manager Usage

```csharp
// Good: Use manager for long-lived applications
var mgr = new SessionManager(loggerFactory);
var session = mgr.Create(profile, httpClient);

// Use session...
await mgr.DisposeAsync(); // Cleans up all sessions

// Avoid: Mixing manager and direct session creation
var mgr = new SessionManager(loggerFactory);
var session1 = mgr.Create(profile, httpClient);
var session2 = SessionBuilder.Build(profile, httpClient, loggerFactory); // Not tracked
```

### 4. Configuration Management

```csharp
// Good: Use strongly-typed configuration
var profile = new HttpClientProfile
{
    Name = "ProductionApi",
    Auth = new AuthSelection.OAuth2(oauthConfig),
    Retry = new RetryOptions { MaxAttempts = 3 },
    Timeout = TimeSpan.FromSeconds(30)
};

// Avoid: Null configurations
var profile = new HttpClientProfile
{
    Name = "Api",
    Auth = new AuthSelection.None(),
    // Missing timeout, retry, etc.
};
``` 