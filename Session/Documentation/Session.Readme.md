# Cymulate.Http.Package.Session

A high-level, **transparent HTTP transport layer** that combines authentication, resilience policies, and rich telemetry under a single `IHttpSession` interface. Use it when you want to send HTTP requests without having to think about tokens, retries, rate-limiters, or circuit-breakers.

## Table of Contents

- [Overview](#overview)
- [Value Proposition](#value-proposition)
- [Use Cases](#use-cases)
- [Architecture](#architecture)
- [Key Concepts](#key-concepts)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Advanced Features](#advanced-features)
- [Telemetry Integration](#telemetry-integration)
- [Best Practices](#best-practices)
- [Dependencies](#dependencies)

## Overview

The Session package provides a unified interface for HTTP communication that abstracts away the complexity of authentication, resilience policies, and observability. It's designed for applications that need reliable, observable, and maintainable HTTP client code.

### Key Features

- **🔐 Unified Authentication**: Support for all authentication strategies (Basic, API Key, Bearer, JWT, OAuth2)
- **🛡️ Built-in Resilience**: Automatic retries, rate limiting, circuit breaking, and timeouts
- **📊 Rich Telemetry**: Comprehensive logging, metrics, and distributed tracing
- **🔄 Automatic Token Refresh**: Background token refresh for OAuth2 and JWT
- **📦 Session Management**: Lifecycle management with automatic cleanup
- **🌊 Streaming Support**: Efficient handling of large request/response bodies
- **⚡ Performance Optimized**: Adaptive rate limiting based on machine capabilities

## Value Proposition

### For Developers
- **Simplified HTTP Client Code**: No more boilerplate for authentication, retries, or error handling
- **Consistent Patterns**: Unified interface across different authentication methods
- **Built-in Best Practices**: Automatic application of resilience patterns
- **Easy Testing**: Mockable interfaces for unit and integration testing

### For Operations
- **Observability**: Complete visibility into HTTP operations with structured logs and metrics
- **Reliability**: Automatic handling of transient failures and rate limits
- **Resource Management**: Automatic cleanup of sessions and connections
- **Performance Monitoring**: Built-in metrics for performance analysis

### For Architecture
- **Separation of Concerns**: Clear boundaries between transport, authentication, and resilience
- **Extensibility**: Easy to add new authentication methods or resilience policies
- **Dependency Management**: Clean dependency injection and resource disposal
- **Scalability**: Efficient session management for high-throughput scenarios

## Use Cases

### 1. API Integration
```csharp
// Simple API client with automatic authentication and retries
var session = SessionBuilder.Build(profile, httpClient, loggerFactory);
var response = await session.ExecuteRequestAsync(request);
```

### 2. File Upload/Download
```csharp
// Efficient streaming for large files
await using var file = File.OpenRead("large.zip");
var response = await session.StreamRequestAsync(request, file);

var streamed = await session.StreamResponseAsync(downloadRequest);
await streamed.ContentStream.CopyToAsync(targetFile);
```

### 3. Microservice Communication
```csharp
// Reliable service-to-service communication with circuit breaking
var sessionManager = new SessionManager(loggerFactory);
var session = sessionManager.Create(profile, httpClient);
// Automatic cleanup and session reuse
```

### 4. Third-Party API Integration
```csharp
// OAuth2 integration with automatic token refresh
var oauthConfig = new OAuth2AuthenticationConfiguration { /* ... */ };
var profile = new HttpClientProfile { Auth = new AuthSelection.OAuth2(oauthConfig) };
// Tokens automatically refreshed in background
```

### 5. High-Throughput Scenarios
```csharp
// Adaptive rate limiting based on machine capabilities
var rateLimiter = new RateLimiterOptions { Kind = RateLimiterKind.FixedWindow };
// Automatically adapts to machine profile
```

## Architecture

### Core Components

#### `IHttpSession`
The main interface that encapsulates all HTTP operations:
```csharp
public interface IHttpSession : IAsyncDisposable
{
    SessionId SessionId { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset LastUsed { get; }
    IAuthenticationProvider Auth { get; }
    IDefensivePolicyRunner Policies { get; }
    
    Task<HttpResponseMessage> ExecuteRequestAsync(HttpRequestMessage request, CancellationToken ct = default);
    Task<HttpResponseMessage> StreamRequestAsync(HttpRequestMessage request, Stream requestBody, ILogger? logger = null, CancellationToken ct = default);
    Task<StreamedResponse> StreamResponseAsync(HttpRequestMessage request, ILogger? logger = null, CancellationToken ct = default);
}
```

#### `SessionBuilder`
Factory class for creating sessions with proper wiring:
```csharp
public static class SessionBuilder
{
    public static HttpSession Build(HttpClientProfile profile, HttpClient client, ILoggerFactory loggerFactory, CancellationToken ct = default);
}
```

#### `SessionManager`
Orchestrator for managing multiple sessions:
```csharp
public sealed class SessionManager : IAsyncDisposable
{
    public IHttpSession Create(HttpClientProfile profile, HttpClient client, CancellationToken ct = default);
    public IHttpSession? Get(SessionId sessionId);
    public bool Remove(SessionId sessionId);
    public IReadOnlyCollection<IHttpSession> All { get; }
}
```

### Data Models

#### `HttpClientProfile`
Configuration container for session behavior:
```csharp
public sealed class HttpClientProfile
{
    public required string Name { get; init; }
    public required AuthSelection Auth { get; init; }
    public TimeoutOptions Timeout { get; init; } = new();
    public RetryOptions? Retry { get; init; }
    public RateLimiterOptions? RateLimiter { get; init; }
    public CircuitBreakerOptions? CircuitBreaker { get; init; }
}
```

#### `SessionId`
Strongly-typed session identifier:
```csharp
public readonly struct SessionId : IEquatable<SessionId>
{
    public Guid Value { get; }
    public static SessionId New() => new(Guid.NewGuid());
}
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| **HttpSession** | Encapsulates an `HttpClient`, authentication provider, and defensive policy runner. Exposes convenience methods for HTTP operations. |
| **SessionBuilder** | Factory that wires up `HttpSession` given a profile, `HttpClient`, and `LoggerFactory`. |
| **SessionManager** | Optional orchestrator that tracks multiple sessions, performs cleanup, and provides lookup helpers. |
| **HttpClientProfile** | Configuration object that defines authentication, timeout, retry, rate limiting, and circuit breaker settings. |
| **Telemetry** | All operations create `Activity` spans and structured logs. Instrumentation can be exported via OpenTelemetry. |

## Quick Start

### 1. Install the Package
```xml
<PackageReference Include="Cymulate.Http.Package.Session" Version="1.0.9" />
```

### 2. Build a Profile
```csharp
var apiKeyCfg = new ApiKeyAuthenticationConfiguration
{
    IsEnabled = true,
    HeaderName = "X-API-Key",
    ApiKey = Environment.GetEnvironmentVariable("MY_API_KEY")!
};

var profile = new HttpClientProfile
{
    Name = "MyApiProfile",
    Auth = new AuthSelection.ApiKey(apiKeyCfg),
    Retry = new RetryOptions
    {
        MaxAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(300),
        BackoffStrategy = RetryBackoff.Exponential
    },
    Timeout = TimeSpan.FromSeconds(20)
};
```

### 3. Create and Use a Session
```csharp
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
var response = await session.ExecuteRequestAsync(request);

Console.WriteLine($"Status: {response.StatusCode}");
```

### 4. Manage Multiple Sessions
```csharp
var mgr = new SessionManager(loggerFactory);

// Create and track
var session = mgr.Create(profile, httpClient);

// Lookup later (updates LastUsed automatically)
var same = mgr.Get(session.SessionId);

// Cleanup
await mgr.DisposeAsync();
```

## Configuration

### Authentication Configuration
```csharp
// API Key
var apiKeyConfig = new ApiKeyAuthenticationConfiguration
{
    IsEnabled = true,
    HeaderName = "X-API-Key",
    ApiKey = "your-api-key"
};

// OAuth2
var oauthConfig = new OAuth2AuthenticationConfiguration
{
    TokenEndpoint = "https://auth.example.com/token",
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    Scope = "api:read api:write"
};

// JWT
var jwtConfig = new JwtAuthenticationConfiguration
{
    Token = "your-jwt-token",
    RefreshEndpoint = "https://auth.example.com/refresh"
};
```

### Resilience Configuration
```csharp
// Retry Policy
var retry = new RetryOptions
{
    MaxAttempts = 3,
    Delay = TimeSpan.FromMilliseconds(300),
    BackoffStrategy = RetryBackoff.Exponential,
    ShouldRetry = (ex, _) => ex is HttpRequestException
};

// Rate Limiting
var rateLimiter = new RateLimiterOptions
{
    Kind = RateLimiterKind.FixedWindow,
    FixedWindow = new FixedWindowOptions
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 10
    }
};

// Circuit Breaker
var circuitBreaker = new CircuitBreakerOptions
{
    FailureThreshold = 5,
    SamplingDuration = TimeSpan.FromSeconds(30),
    MinimumThroughput = 2,
    BreakDuration = TimeSpan.FromSeconds(60)
};
```

## Advanced Features

### Custom Rate Limiting per Request
```csharp
var customLimiter = new RateLimiterPolicy(new RateLimiterOptions { /* ... */ });
request.Options.Set(RequestOptionKeys.CustomRateLimiterKey, customLimiter);
var response = await session.ExecuteRequestAsync(request);
```

### Streaming Large Files
```csharp
// Upload large file
await using var file = File.OpenRead("large.zip");
var uploadReq = new HttpRequestMessage(HttpMethod.Post, "/upload");
var uploadResp = await session.StreamRequestAsync(uploadReq, file);

// Download large file
var downloadReq = new HttpRequestMessage(HttpMethod.Get, "/big-file");
var streamed = await session.StreamResponseAsync(downloadReq);
await using var target = File.Create("download.zip");
await streamed.ContentStream.CopyToAsync(target);
```

### Session Lifecycle Management
```csharp
var mgr = new SessionManager(loggerFactory);

// Get sessions by last used (for LRU cleanup)
var sessionsByLastUsed = mgr.GetSessionsByLastUsed();

// Get idle sessions
var idleSessions = mgr.GetIdleSessions(DateTimeOffset.UtcNow.AddMinutes(-30));

// Manual cleanup
foreach (var session in idleSessions)
{
    await session.DisposeAsync();
    mgr.Remove(session.SessionId);
}
```

## Telemetry Integration

### OpenTelemetry Setup
```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Authentication")
    .AddSource("DefensivePolicyRunner")
    .AddSource("Session")
    .AddHttpClientInstrumentation()
    .AddJaegerExporter()
    .Build();
```

### Available Metrics
- `session.created` - Number of sessions created
- `session.disposed` - Number of sessions disposed
- `session.lifetime_seconds` - Session lifetime distribution
- `session.active_count` - Current active sessions
- `session.refresh_attempts` - Token refresh attempts
- `session.cleanup_duration_seconds` - Cleanup operation duration

### Available Traces
- `session.create` - Session creation
- `session.execute_request` - HTTP request execution
- `session.refresh_token` - Token refresh operations
- `session.cleanup` - Session cleanup operations

## Best Practices

### 1. Resource Management
```csharp
// Always use await using for sessions
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// Dispose HttpClient after session
await session.DisposeAsync();
httpClient.Dispose();
```

### 2. Session Reuse
```csharp
// Use SessionManager for long-lived applications
var mgr = new SessionManager(loggerFactory);
var session = mgr.Create(profile, httpClient);

// Reuse the same session for multiple requests
for (int i = 0; i < 100; i++)
{
    var response = await session.ExecuteRequestAsync(request);
}
```

### 3. Error Handling
```csharp
try
{
    var response = await session.ExecuteRequestAsync(request);
    // Handle success
}
catch (HttpRequestException ex)
{
    // Handle network errors (retries handled automatically)
}
catch (TaskCanceledException ex)
{
    // Handle timeout (configured in profile)
}
```

### 4. Configuration Management
```csharp
// Use strongly-typed configuration
var profile = new HttpClientProfile
{
    Name = "ProductionApi",
    Auth = new AuthSelection.OAuth2(oauthConfig),
    Retry = new RetryOptions { MaxAttempts = 3 },
    Timeout = TimeSpan.FromSeconds(30)
};
```

## Dependencies

The Session package depends on:
- `Cymulate.Http.Package.Authentication` (v1.0.3)
- `Cymulate.Http.Package.DefensiveToolkit` (v1.0.5)

These dependencies provide:
- Authentication strategies and providers
- Resilience policies (retry, rate limiting, circuit breaker)
- Machine profile adaptation
- Telemetry and diagnostics

## License

&copy; Cymulate — MIT-licensed. See repository root for full license text. 