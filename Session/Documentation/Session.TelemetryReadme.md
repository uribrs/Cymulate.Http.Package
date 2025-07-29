# Session Telemetry - Observability and Monitoring

This document covers the telemetry and observability features of the Session package, including metrics, distributed tracing, and logging capabilities.

## Table of Contents

- [Overview](#overview)
- [Telemetry Architecture](#telemetry-architecture)
- [Metrics](#metrics)
- [Distributed Tracing](#distributed-tracing)
- [Logging](#logging)
- [OpenTelemetry Integration](#opentelemetry-integration)
- [Telemetry Configuration](#telemetry-configuration)
- [Monitoring and Alerting](#monitoring-and-alerting)
- [Performance Impact](#performance-impact)

## Overview

The Session package provides comprehensive observability through:

- **📊 Metrics**: Counters, histograms, and gauges for performance monitoring
- **🔍 Distributed Tracing**: Activity spans for request flow analysis
- **📝 Structured Logging**: Detailed logs with correlation IDs
- **📈 Performance Monitoring**: Automatic collection of key performance indicators

### Key Features

- **Automatic Instrumentation**: All operations are automatically instrumented
- **Correlation**: Session IDs and request IDs link all telemetry data
- **Low Overhead**: Efficient telemetry collection with minimal performance impact
- **OpenTelemetry Compatible**: Standard observability format for integration

## Telemetry Architecture

### Components

```csharp
// Core telemetry components
SessionDiagnostics      // Distributed tracing (Activity)
SessionMetrics         // Metrics collection (Meter)
SessionTelemetryConstants // Constants and tags
```

### Data Flow

```
HTTP Request → Session → Authentication → Defensive Policies → Transport
     ↓              ↓           ↓              ↓                ↓
  Activity      Metrics     Logging       Tracing          Telemetry
     ↓              ↓           ↓              ↓                ↓
OpenTelemetry → Metrics → Structured → Distributed → Observability
  Pipeline      Export     Logs        Traces       Platform
```

## Metrics

The Session package exposes comprehensive metrics for monitoring session behavior and performance.

### Available Metrics

#### Counters

| Metric Name | Description | Tags |
|-------------|-------------|------|
| `session.created` | Number of sessions created | `profile.name`, `auth.type` |
| `session.disposed` | Number of sessions disposed | `auth.type`, `refresh.attempts` |
| `session.removed` | Number of sessions manually removed | - |
| `session.cleaned_up` | Number of sessions cleaned up | - |
| `session.refresh_attempts` | Token refresh attempts | `auth.type`, `reason` |
| `session.refresh_failures` | Token refresh failures | `auth.type`, `reason` |
| `session.last_used_updates` | Last used timestamp updates | - |

#### Histograms

| Metric Name | Description | Unit | Tags |
|-------------|-------------|------|------|
| `session.lifetime_seconds` | Session lifetime distribution | seconds | `auth.type` |
| `session.cleanup_duration_seconds` | Cleanup operation duration | seconds | - |
| `session.refresh_backoff_seconds` | Time between refresh attempts | seconds | - |

#### Gauges

| Metric Name | Description | Tags |
|-------------|-------------|------|
| `session.active_count` | Current number of active sessions | - |

### Metrics Collection

```csharp
// Metrics are automatically collected
public static void RecordSessionCreated(string profileName, string authType)
{
    SessionsCreated.Add(1,
        new KeyValuePair<string, object?>("profile.name", profileName),
        new KeyValuePair<string, object?>("auth.type", authType));
}

public static void RecordSessionDisposed(double lifetimeSeconds, string authType, int refreshAttempts)
{
    SessionsDisposed.Add(1,
        new KeyValuePair<string, object?>("auth.type", authType),
        new KeyValuePair<string, object?>("refresh.attempts", refreshAttempts));

    SessionLifetime.Record(lifetimeSeconds,
        new KeyValuePair<string, object?>("auth.type", authType));
}
```

### Metrics Usage Examples

#### Prometheus Integration

```csharp
// Configure Prometheus metrics collection
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("PowerHttp.Session")
    .AddPrometheusExporter()
    .Build();
```

#### Custom Metrics Dashboard

```csharp
// Query metrics for dashboard
var activeSessions = await metricsClient.GetGaugeValueAsync("session.active_count");
var sessionLifetime = await metricsClient.GetHistogramAsync("session.lifetime_seconds");
var refreshFailures = await metricsClient.GetCounterValueAsync("session.refresh_failures");
```

## Distributed Tracing

The Session package provides comprehensive distributed tracing using OpenTelemetry Activity.

### Activity Sources

```csharp
// Session activity source
public static readonly ActivitySource Source = 
    new(SessionTelemetryConstants.ActivitySourceName); // "PowerHttp.Session"
```

### Available Operations

| Operation | Description | Tags |
|-----------|-------------|------|
| `session.create` | Session creation | `profile.name`, `auth.type` |
| `session.get` | Session lookup | `session.id` |
| `session.remove` | Session removal | `session.id` |
| `session.cleanup` | Session cleanup | `sessions.cleaned_up`, `cleanup.duration_ms` |
| `session.refresh_token` | Token refresh | `refresh.attempts`, `refresh.success` |
| `session.update_last_used` | Last used update | `session.id` |
| `session.dispose` | Session disposal | `session.id`, `session.auto_refresh` |
| `session.execute_request` | HTTP request execution | `session.id`, `auth.type`, `response.status_code` |

### Tracing Implementation

```csharp
public static Activity? StartSessionActivity(string operationName)
{
    var activity = Source.StartActivity($"Session.{operationName}", ActivityKind.Internal);
    return activity;
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
```

### Trace Examples

#### Session Creation Trace

```csharp
// Trace: session.create
var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.CreateSession);

SessionDiagnostics.RecordProfileInfo(activity, profile.Name, authType);
SessionDiagnostics.RecordSessionInfo(activity, session.SessionId, session.CreatedAt, session.LastUsed);
SessionDiagnostics.RecordSessionCount(activity, _sessions.Count);
SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
```

#### Request Execution Trace

```csharp
// Trace: session.execute_request
using var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.ExecuteRequest);

SessionDiagnostics.RecordSessionInfo(activity, _session.SessionId, _session.CreatedAt, _session.LastUsed);
SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionId, _session.SessionId.ToString());
SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.AuthType, _authType);

// ... execute request ...

SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.ResponseStatusCode, ((int)response.StatusCode).ToString());
SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
```

## Logging

The Session package provides structured logging with correlation IDs and detailed context.

### Log Categories

| Category | Description | Log Level |
|----------|-------------|-----------|
| `Session` | General session operations | Info/Debug |
| `SessionManager` | Session management operations | Info/Debug |
| `RequestStreamer` | Request streaming operations | Info/Debug |
| `ResponseStreamer` | Response streaming operations | Info/Debug |

### Log Examples

#### Session Creation

```csharp
_logger.LogInformation("[SessionManager] Session created: {SessionId}", session.SessionId);
```

#### Request Execution

```csharp
_logger.LogInformation("[HttpSession:{SessionId}] Authenticating request to {Uri}", _session.SessionId, request.RequestUri);
_logger.LogInformation("[HttpSession:{SessionId}] Executing HTTP request to {Uri}", _session.SessionId, request.RequestUri);
_logger.LogInformation("[HttpSession:{SessionId}] Received {StatusCode} from {Uri}", _session.SessionId, response.StatusCode, request.RequestUri);
```

#### Token Refresh

```csharp
_logger.LogInformation("[HttpSession:{SessionId}] Token expiring in {Seconds}s — refreshing", SessionId, timeLeft.TotalSeconds);
_logger.LogInformation("[HttpSession:{SessionId}] Token refreshed. New expiry: {ExpiresAt}", SessionId, refreshable.ExpiresAt);
```

#### Error Logging

```csharp
_logger.LogError(ex, "[HttpSession:{SessionId}] Request execution failed", _session.SessionId);
_logger.LogWarning(ex, "[HttpSession:{SessionId}] Refresh loop error", SessionId);
```

### Structured Logging

```csharp
// Structured log with correlation
_logger.LogInformation(
    "Session operation completed",
    new Dictionary<string, object>
    {
        ["session_id"] = session.SessionId.ToString(),
        ["operation"] = "create",
        ["duration_ms"] = stopwatch.ElapsedMilliseconds,
        ["auth_type"] = authType
    });
```

## OpenTelemetry Integration

### Setup

```csharp
// Configure OpenTelemetry for Session package
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Authentication")
    .AddSource("DefensivePolicyRunner")
    .AddSource("Session") // Session activity source
    .AddHttpClientInstrumentation()
    .AddJaegerExporter()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("PowerHttp.Session")
    .AddPrometheusExporter()
    .Build();
```

### Exporters

#### Jaeger (Distributed Tracing)

```csharp
.AddJaegerExporter(options =>
{
    options.AgentHost = "localhost";
    options.AgentPort = 6831;
})
```

#### Prometheus (Metrics)

```csharp
.AddPrometheusExporter(options =>
{
    options.HttpListenerPrefixes = new string[] { "http://localhost:9184/" };
})
```

#### Console (Development)

```csharp
.AddConsoleExporter()
```

### Correlation

All telemetry data is correlated using:

- **Session ID**: Links all operations for a specific session
- **Request ID**: Links HTTP requests across services
- **Trace ID**: Links distributed traces across services

```csharp
// Correlation in traces
activity.SetTag("session.id", sessionId.ToString());
activity.SetTag("request.id", requestId.ToString());
activity.SetTag("trace.id", activity.TraceId.ToString());
```

## Telemetry Configuration

### Activity Sampling

```csharp
// Configure activity sampling
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Session")
    .SetSampler(new AlwaysOnSampler()) // or ParentBasedSampler
    .Build();
```

### Metrics Filtering

```csharp
// Filter metrics by tags
.AddMeter("PowerHttp.Session")
.AddView(instrument =>
{
    if (instrument.Name == "session.lifetime_seconds")
    {
        return new HistogramConfiguration
        {
            Name = "session.lifetime_seconds",
            Description = "Session lifetime in seconds",
            Unit = "s",
            BucketBoundaries = new double[] { 1, 5, 10, 30, 60, 300, 600, 1800, 3600 }
        };
    }
    return null;
})
```

### Logging Configuration

```csharp
// Configure structured logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Information)
        .AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        })
        .AddSeq(options =>
        {
            options.ServerUrl = "http://localhost:5341";
        });
});
```

## Monitoring and Alerting

### Key Performance Indicators (KPIs)

#### Session Health

```yaml
# Prometheus alert rules
groups:
  - name: session_alerts
    rules:
      - alert: HighSessionCreationRate
        expr: rate(session_created_total[5m]) > 10
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High session creation rate detected"
          
      - alert: SessionRefreshFailures
        expr: rate(session_refresh_failures_total[5m]) > 0.1
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Session token refresh failures detected"
          
      - alert: LongSessionLifetime
        expr: histogram_quantile(0.95, session_lifetime_seconds) > 3600
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Sessions living longer than expected"
```

#### Resource Usage

```yaml
      - alert: TooManyActiveSessions
        expr: session_active_count > 1000
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "Too many active sessions"
          
      - alert: SessionCleanupSlow
        expr: histogram_quantile(0.95, session_cleanup_duration_seconds) > 5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Session cleanup taking too long"
```

### Dashboard Queries

#### Grafana Dashboard

```sql
-- Session creation rate
rate(session_created_total[5m])

-- Active sessions over time
session_active_count

-- Session lifetime distribution
histogram_quantile(0.95, session_lifetime_seconds)

-- Refresh failure rate
rate(session_refresh_failures_total[5m])

-- Cleanup duration
histogram_quantile(0.95, session_cleanup_duration_seconds)
```

### Health Checks

```csharp
public class SessionHealthCheck : IHealthCheck
{
    private readonly SessionManager _sessionManager;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var activeSessions = _sessionManager.All.Count;
            
            if (activeSessions > 1000)
            {
                return HealthCheckResult.Degraded($"Too many active sessions: {activeSessions}");
            }
            
            return HealthCheckResult.Healthy($"Active sessions: {activeSessions}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Session manager health check failed", ex);
        }
    }
}
```

## Performance Impact

### Telemetry Overhead

The telemetry system is designed for minimal performance impact:

- **Sampling**: Activities can be sampled to reduce overhead
- **Async Processing**: Telemetry operations are asynchronous
- **Conditional Execution**: Telemetry is only collected when enabled

### Performance Optimization

```csharp
// Conditional telemetry collection
public static void RecordSessionInfo(Activity? activity, SessionId sessionId, DateTimeOffset createdAt, DateTimeOffset lastUsed)
{
    if (activity?.IsAllDataRequested == true) // Only collect when sampling
    {
        activity.SetTag(SessionTelemetryConstants.Tags.SessionId, sessionId.ToString());
        activity.SetTag(SessionTelemetryConstants.Tags.SessionCreatedAt, createdAt.ToString("O"));
        activity.SetTag(SessionTelemetryConstants.Tags.SessionLastUsed, lastUsed.ToString("O"));
    }
}
```

### Configuration Recommendations

#### Production

```csharp
// Production telemetry configuration
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Session")
    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(0.1))) // 10% sampling
    .AddOtlpExporter() // Send to observability platform
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("PowerHttp.Session")
    .AddOtlpExporter()
    .Build();
```

#### Development

```csharp
// Development telemetry configuration
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Session")
    .SetSampler(new AlwaysOnSampler()) // 100% sampling
    .AddConsoleExporter() // Local development
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("PowerHttp.Session")
    .AddConsoleExporter()
    .Build();
```

### Monitoring Telemetry Performance

```csharp
// Monitor telemetry overhead
var telemetryMetrics = new Meter("PowerHttp.Telemetry");

var activityCreationTime = telemetryMetrics.CreateHistogram<double>(
    "activity_creation_time_ms",
    unit: "ms",
    description: "Time to create and configure activities");

var metricsRecordingTime = telemetryMetrics.CreateHistogram<double>(
    "metrics_recording_time_ms",
    unit: "ms",
    description: "Time to record metrics");
``` 