# Session Examples - Comprehensive Usage Guide

This document provides comprehensive examples for using the Session package in various scenarios, from simple API calls to complex enterprise applications.

## Table of Contents

- [Overview](#overview)
- [Basic Examples](#basic-examples)
- [Authentication Examples](#authentication-examples)
- [Resilience Examples](#resilience-examples)
- [Streaming Examples](#streaming-examples)
- [Session Management Examples](#session-management-examples)
- [Enterprise Examples](#enterprise-examples)
- [Testing Examples](#testing-examples)
- [Performance Examples](#performance-examples)

## Overview

This guide provides practical examples for:

- **Basic Usage**: Simple HTTP requests with authentication
- **Authentication**: All supported authentication methods
- **Resilience**: Retry, rate limiting, and circuit breaker patterns
- **Streaming**: Large file uploads and downloads
- **Session Management**: Multi-session scenarios
- **Enterprise**: Production-ready configurations
- **Testing**: Unit and integration testing patterns
- **Performance**: High-throughput and optimization scenarios

## Basic Examples

### Simple API Call

```csharp
// 1. Create a basic profile
var profile = new HttpClientProfile
{
    Name = "SimpleApi",
    Auth = new AuthSelection.None(),
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Create session
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// 3. Make request
var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
var response = await session.ExecuteRequestAsync(request);

Console.WriteLine($"Status: {response.StatusCode}");
var content = await response.Content.ReadAsStringAsync();
```

### API Key Authentication

```csharp
// 1. Configure API key authentication
var apiKeyConfig = new ApiKeyAuthenticationConfiguration
{
    IsEnabled = true,
    HeaderName = "X-API-Key",
    ApiKey = Environment.GetEnvironmentVariable("API_KEY")!
};

var profile = new HttpClientProfile
{
    Name = "ApiKeyExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Create session and make request
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/users")
{
    Content = new StringContent(JsonSerializer.Serialize(userData), Encoding.UTF8, "application/json")
};

var response = await session.ExecuteRequestAsync(request);
```

### Basic Error Handling

```csharp
try
{
    var response = await session.ExecuteRequestAsync(request);
    
    if (response.IsSuccessStatusCode)
    {
        var data = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Success: {data}");
    }
    else
    {
        Console.WriteLine($"HTTP Error: {response.StatusCode}");
    }
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Network error: {ex.Message}");
}
catch (TaskCanceledException ex)
{
    Console.WriteLine($"Timeout: {ex.Message}");
}
catch (ObjectDisposedException ex)
{
    Console.WriteLine($"Session disposed: {ex.Message}");
}
```

## Authentication Examples

### OAuth2 Authentication

```csharp
// 1. Configure OAuth2
var oauthConfig = new OAuth2AuthenticationConfiguration
{
    TokenEndpoint = "https://auth.example.com/oauth/token",
    ClientId = Environment.GetEnvironmentVariable("OAUTH_CLIENT_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("OAUTH_CLIENT_SECRET")!,
    Scope = "api:read api:write",
    GrantType = "client_credentials"
};

var profile = new HttpClientProfile
{
    Name = "OAuth2Example",
    Auth = new AuthSelection.OAuth2(oauthConfig),
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Create session (tokens automatically refreshed)
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// 3. Make authenticated requests
for (int i = 0; i < 10; i++)
{
    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/protected-data");
    var response = await session.ExecuteRequestAsync(request);
    
    // Token refresh happens automatically in background
    Console.WriteLine($"Request {i}: {response.StatusCode}");
    
    await Task.Delay(1000); // Wait between requests
}
```

### JWT Authentication

```csharp
// 1. Configure JWT with refresh
var jwtConfig = new JwtAuthenticationConfiguration
{
    Token = Environment.GetEnvironmentVariable("JWT_TOKEN")!,
    RefreshEndpoint = "https://auth.example.com/refresh",
    RefreshToken = Environment.GetEnvironmentVariable("REFRESH_TOKEN")!
};

var profile = new HttpClientProfile
{
    Name = "JwtExample",
    Auth = new AuthSelection.Jwt(jwtConfig),
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Create session
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// 3. Use session (JWT automatically refreshed when needed)
var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/user/profile");
var response = await session.ExecuteRequestAsync(request);
```

### Basic Authentication

```csharp
// 1. Configure Basic Auth
var basicConfig = new BasicAuthenticationConfiguration
{
    Username = Environment.GetEnvironmentVariable("API_USERNAME")!,
    Password = Environment.GetEnvironmentVariable("API_PASSWORD")!
};

var profile = new HttpClientProfile
{
    Name = "BasicAuthExample",
    Auth = new AuthSelection.Basic(basicConfig),
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Create session and use
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/admin/users");
var response = await session.ExecuteRequestAsync(request);
```

### Bearer Token Authentication

```csharp
// 1. Configure Bearer token
var bearerConfig = new BearerAuthenticationConfiguration
{
    Token = Environment.GetEnvironmentVariable("BEARER_TOKEN")!
};

var profile = new HttpClientProfile
{
    Name = "BearerExample",
    Auth = new AuthSelection.Bearer(bearerConfig),
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Create session and use
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
var response = await session.ExecuteRequestAsync(request);
```

## Resilience Examples

### Retry Policy

```csharp
// 1. Configure retry policy
var retryOptions = new RetryOptions
{
    MaxAttempts = 3,
    Delay = TimeSpan.FromMilliseconds(200),
    BackoffStrategy = RetryBackoff.Exponential,
    ShouldRetry = (ex, attempt) =>
    {
        // Retry on network errors and 5xx responses
        if (ex is HttpRequestException) return true;
        if (ex is HttpRequestException httpEx && httpEx.StatusCode >= 500) return true;
        return false;
    }
};

var profile = new HttpClientProfile
{
    Name = "RetryExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    Retry = retryOptions,
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Use session with retry
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

var request = new HttpRequestMessage(HttpMethod.Get, "https://unreliable-api.example.com/data");
var response = await session.ExecuteRequestAsync(request);
// Automatically retries on failures
```

### Rate Limiting

```csharp
// 1. Configure rate limiting
var rateLimiterOptions = new RateLimiterOptions
{
    Kind = RateLimiterKind.FixedWindow,
    FixedWindow = new FixedWindowOptions
    {
        PermitLimit = 100,           // 100 requests
        Window = TimeSpan.FromMinutes(1), // per minute
        QueueLimit = 10              // queue up to 10 requests
    }
};

var profile = new HttpClientProfile
{
    Name = "RateLimitExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    RateLimiter = rateLimiterOptions,
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Use session with rate limiting
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// Make many requests (automatically rate limited)
var tasks = new List<Task<HttpResponseMessage>>();
for (int i = 0; i < 150; i++)
{
    var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.example.com/data/{i}");
    tasks.Add(session.ExecuteRequestAsync(request));
}

var responses = await Task.WhenAll(tasks);
```

### Circuit Breaker

```csharp
// 1. Configure circuit breaker
var circuitBreakerOptions = new CircuitBreakerOptions
{
    FailureThreshold = 5,           // 5 failures
    SamplingDuration = TimeSpan.FromSeconds(30), // in 30 seconds
    MinimumThroughput = 2,          // minimum 2 requests
    BreakDuration = TimeSpan.FromSeconds(60)     // break for 60 seconds
};

var profile = new HttpClientProfile
{
    Name = "CircuitBreakerExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    CircuitBreaker = circuitBreakerOptions,
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Use session with circuit breaker
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// Circuit breaker automatically opens on repeated failures
for (int i = 0; i < 10; i++)
{
    try
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://failing-api.example.com/data");
        var response = await session.ExecuteRequestAsync(request);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Request {i} failed: {ex.Message}");
    }
}
```

### Combined Resilience

```csharp
// 1. Configure comprehensive resilience
var profile = new HttpClientProfile
{
    Name = "ResilientExample",
    Auth = new AuthSelection.OAuth2(oauthConfig),
    Retry = new RetryOptions
    {
        MaxAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(200),
        BackoffStrategy = RetryBackoff.Exponential
    },
    RateLimiter = new RateLimiterOptions
    {
        Kind = RateLimiterKind.SlidingWindow,
        SlidingWindow = new SlidingWindowOptions
        {
            PermitLimit = 50,
            Window = TimeSpan.FromSeconds(30),
            SegmentsPerWindow = 3
        }
    },
    CircuitBreaker = new CircuitBreakerOptions
    {
        FailureThreshold = 3,
        SamplingDuration = TimeSpan.FromSeconds(60),
        MinimumThroughput = 1,
        BreakDuration = TimeSpan.FromSeconds(30)
    },
    Timeout = TimeSpan.FromSeconds(30)
};

// 2. Use resilient session
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// All resilience policies work together automatically
var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/critical-data");
var response = await session.ExecuteRequestAsync(request);
```

## Streaming Examples

### File Upload

```csharp
// 1. Configure session for file upload
var profile = new HttpClientProfile
{
    Name = "FileUploadExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    Timeout = TimeSpan.FromMinutes(10) // Longer timeout for large files
};

await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// 2. Upload large file
await using var file = File.OpenRead("large-dataset.zip");
var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/upload");

var response = await session.StreamRequestAsync(uploadRequest, file);
Console.WriteLine($"Upload completed: {response.StatusCode}");
```

### File Download

```csharp
// 1. Configure session for file download
var profile = new HttpClientProfile
{
    Name = "FileDownloadExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    Timeout = TimeSpan.FromMinutes(10)
};

await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// 2. Download large file
var downloadRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/download/large-file");
var streamed = await session.StreamResponseAsync(downloadRequest);

await using var targetFile = File.Create("downloaded-file.zip");
await streamed.ContentStream.CopyToAsync(targetFile);

Console.WriteLine($"Download completed: {streamed.RawResponse.StatusCode}");
```

### Memory-Efficient Processing

```csharp
// Process large JSON response without loading into memory
var profile = new HttpClientProfile
{
    Name = "StreamingExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    Timeout = TimeSpan.FromMinutes(5)
};

await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/large-dataset");
var streamed = await session.StreamResponseAsync(request);

using var reader = new StreamReader(streamed.ContentStream);
using var jsonReader = new JsonTextReader(reader);
using var jsonSerializer = new JsonSerializer();

var processedCount = 0;
while (jsonReader.Read())
{
    if (jsonReader.TokenType == JsonToken.StartObject)
    {
        var item = jsonSerializer.Deserialize<DataItem>(jsonReader);
        await ProcessItemAsync(item);
        processedCount++;
        
        if (processedCount % 1000 == 0)
        {
            Console.WriteLine($"Processed {processedCount} items");
        }
    }
}
```

### Concurrent Streaming

```csharp
// Upload multiple files concurrently
var profile = new HttpClientProfile
{
    Name = "ConcurrentUploadExample",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    RateLimiter = new RateLimiterOptions
    {
        Kind = RateLimiterKind.TokenBucket,
        TokenBucket = new TokenBucketOptions
        {
            TokenBucketCapacity = 5,
            TokenBucketRefillRate = 1,
            QueueLimit = 10
        }
    }
};

await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

var files = Directory.GetFiles("uploads", "*.zip");
var uploadTasks = new List<Task<HttpResponseMessage>>();

foreach (var filePath in files)
{
    var uploadTask = Task.Run(async () =>
    {
        await using var file = File.OpenRead(filePath);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/upload");
        return await session.StreamRequestAsync(request, file);
    });
    
    uploadTasks.Add(uploadTask);
}

var responses = await Task.WhenAll(uploadTasks);
Console.WriteLine($"Uploaded {responses.Length} files");
```

## Session Management Examples

### Session Manager Usage

```csharp
// 1. Create session manager
var sessionManager = new SessionManager(loggerFactory);

// 2. Create multiple sessions
var apiKeyProfile = new HttpClientProfile
{
    Name = "ApiKeySession",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    Timeout = TimeSpan.FromSeconds(30)
};

var oauthProfile = new HttpClientProfile
{
    Name = "OAuthSession",
    Auth = new AuthSelection.OAuth2(oauthConfig),
    Timeout = TimeSpan.FromSeconds(30)
};

var apiKeySession = sessionManager.Create(apiKeyProfile, httpClient);
var oauthSession = sessionManager.Create(oauthProfile, httpClient);

// 3. Use sessions
var apiKeyRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/public-data");
var apiKeyResponse = await apiKeySession.ExecuteRequestAsync(apiKeyRequest);

var oauthRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/private-data");
var oauthResponse = await oauthSession.ExecuteRequestAsync(oauthRequest);

// 4. Retrieve sessions later
var sameApiKeySession = sessionManager.Get(apiKeySession.SessionId);
var sameOauthSession = sessionManager.Get(oauthSession.SessionId);

// 5. Cleanup
await sessionManager.DisposeAsync();
```

### Session Lifecycle Management

```csharp
// Manage session lifecycle with cleanup
var sessionManager = new SessionManager(loggerFactory, TimeSpan.FromMinutes(2));

// Create sessions
var sessions = new List<IHttpSession>();
for (int i = 0; i < 10; i++)
{
    var profile = new HttpClientProfile
    {
        Name = $"Session{i}",
        Auth = new AuthSelection.ApiKey(apiKeyConfig),
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    var session = sessionManager.Create(profile, httpClient);
    sessions.Add(session);
}

// Use some sessions
for (int i = 0; i < 5; i++)
{
    var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.example.com/data/{i}");
    await sessions[i].ExecuteRequestAsync(request);
}

// Get idle sessions (not used in last 30 minutes)
var idleThreshold = DateTimeOffset.UtcNow.AddMinutes(-30);
var idleSessions = sessionManager.GetIdleSessions(idleThreshold);

// Clean up idle sessions
foreach (var session in idleSessions)
{
    await session.DisposeAsync();
    sessionManager.Remove(session.SessionId);
}

// Get sessions by last used (for LRU cleanup)
var sessionsByLastUsed = sessionManager.GetSessionsByLastUsed();
var oldestSessions = sessionsByLastUsed.Take(3);

foreach (var session in oldestSessions)
{
    await session.DisposeAsync();
    sessionManager.Remove(session.SessionId);
}
```

### Session Reuse Pattern

```csharp
// Efficient session reuse for multiple requests
var sessionManager = new SessionManager(loggerFactory);

var profile = new HttpClientProfile
{
    Name = "ReusableSession",
    Auth = new AuthSelection.OAuth2(oauthConfig),
    Retry = new RetryOptions { MaxAttempts = 3 },
    Timeout = TimeSpan.FromSeconds(30)
};

// Create session once
var session = sessionManager.Create(profile, httpClient);

// Reuse for multiple requests
var tasks = new List<Task<HttpResponseMessage>>();
for (int i = 0; i < 100; i++)
{
    var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.example.com/users/{i}");
    tasks.Add(session.ExecuteRequestAsync(request));
}

var responses = await Task.WhenAll(tasks);
Console.WriteLine($"Completed {responses.Length} requests with single session");

// Cleanup
await sessionManager.DisposeAsync();
```

## Enterprise Examples

### Dependency Injection Setup

```csharp
// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Configure HttpClient
    services.AddHttpClient("ApiClient", client =>
    {
        client.BaseAddress = new Uri("https://api.example.com/");
        client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
    });

    // Configure Session Manager
    services.AddSingleton<SessionManager>(provider =>
    {
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        return new SessionManager(loggerFactory, TimeSpan.FromMinutes(5));
    });

    // Configure profiles
    services.Configure<HttpClientProfile>("ApiKeyProfile", configuration.GetSection("ApiKeyProfile"));
    services.Configure<HttpClientProfile>("OAuthProfile", configuration.GetSection("OAuthProfile"));
}

// appsettings.json
{
  "ApiKeyProfile": {
    "Name": "ApiKeyProfile",
    "Auth": {
      "ApiKey": {
        "IsEnabled": true,
        "HeaderName": "X-API-Key",
        "ApiKey": "your-api-key"
      }
    },
    "Retry": {
      "MaxAttempts": 3,
      "Delay": "00:00:00.200",
      "BackoffStrategy": "Exponential"
    },
    "Timeout": "00:00:30"
  }
}
```

### Service Layer Implementation

```csharp
public class ApiService
{
    private readonly SessionManager _sessionManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<HttpClientProfile> _profileOptions;
    private readonly ILogger<ApiService> _logger;

    public ApiService(
        SessionManager sessionManager,
        IHttpClientFactory httpClientFactory,
        IOptions<HttpClientProfile> profileOptions,
        ILogger<ApiService> logger)
    {
        _sessionManager = sessionManager;
        _httpClientFactory = httpClientFactory;
        _profileOptions = profileOptions;
        _logger = logger;
    }

    public async Task<UserData> GetUserAsync(int userId)
    {
        var httpClient = _httpClientFactory.CreateClient("ApiClient");
        var session = _sessionManager.Create(_profileOptions.Value, httpClient);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/users/{userId}");
            var response = await session.ExecuteRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<UserData>(content);
            }

            throw new ApiException($"Failed to get user: {response.StatusCode}");
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    public async Task<Stream> DownloadFileAsync(string fileId)
    {
        var httpClient = _httpClientFactory.CreateClient("ApiClient");
        var session = _sessionManager.Create(_profileOptions.Value, httpClient);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/files/{fileId}/download");
            var streamed = await session.StreamResponseAsync(request);
            return streamed.ContentStream;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }
}
```

### Health Monitoring

```csharp
public class SessionHealthCheck : IHealthCheck
{
    private readonly SessionManager _sessionManager;
    private readonly ILogger<SessionHealthCheck> _logger;

    public SessionHealthCheck(SessionManager sessionManager, ILogger<SessionHealthCheck> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var activeSessions = _sessionManager.All.Count;
            
            // Check for too many active sessions
            if (activeSessions > 1000)
            {
                _logger.LogWarning("Too many active sessions: {Count}", activeSessions);
                return HealthCheckResult.Degraded($"Too many active sessions: {activeSessions}");
            }

            // Check for idle sessions
            var idleThreshold = DateTimeOffset.UtcNow.AddMinutes(-30);
            var idleSessions = _sessionManager.GetIdleSessions(idleThreshold);
            
            if (idleSessions.Count > 100)
            {
                _logger.LogWarning("Too many idle sessions: {Count}", idleSessions.Count);
                return HealthCheckResult.Degraded($"Too many idle sessions: {idleSessions.Count}");
            }

            return HealthCheckResult.Healthy($"Active sessions: {activeSessions}, Idle sessions: {idleSessions.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session health check failed");
            return HealthCheckResult.Unhealthy("Session health check failed", ex);
        }
    }
}
```

## Testing Examples

### Unit Testing

```csharp
[Test]
public async Task ExecuteRequestAsync_WithValidRequest_ReturnsSuccess()
{
    // Arrange
    var mockHttpClient = new MockHttpMessageHandler();
    mockHttpClient.When("https://api.example.com/data")
        .Respond(HttpStatusCode.OK, "application/json", "{\"result\":\"success\"}");

    var httpClient = new HttpClient(mockHttpClient);
    var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

    var profile = new HttpClientProfile
    {
        Name = "TestProfile",
        Auth = new AuthSelection.None(),
        Timeout = TimeSpan.FromSeconds(30)
    };

    await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

    // Act
    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
    var response = await session.ExecuteRequestAsync(request);

    // Assert
    Assert.That(response.IsSuccessStatusCode, Is.True);
    var content = await response.Content.ReadAsStringAsync();
    Assert.That(content, Is.EqualTo("{\"result\":\"success\"}"));
}
```

### Integration Testing

```csharp
[Test]
public async Task OAuth2Session_WithValidCredentials_RefreshesToken()
{
    // Arrange
    var mockHttpClient = new MockHttpMessageHandler();
    
    // Mock token endpoint
    mockHttpClient.When("https://auth.example.com/oauth/token")
        .Respond(HttpStatusCode.OK, "application/json", 
            "{\"access_token\":\"new-token\",\"expires_in\":3600}");

    // Mock API endpoint
    mockHttpClient.When("https://api.example.com/protected")
        .Respond(HttpStatusCode.OK, "application/json", "{\"data\":\"protected\"}");

    var httpClient = new HttpClient(mockHttpClient);
    var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

    var oauthConfig = new OAuth2AuthenticationConfiguration
    {
        TokenEndpoint = "https://auth.example.com/oauth/token",
        ClientId = "test-client",
        ClientSecret = "test-secret",
        Scope = "api:read"
    };

    var profile = new HttpClientProfile
    {
        Name = "TestOAuth",
        Auth = new AuthSelection.OAuth2(oauthConfig),
        Timeout = TimeSpan.FromSeconds(30)
    };

    await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

    // Act
    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/protected");
    var response = await session.ExecuteRequestAsync(request);

    // Assert
    Assert.That(response.IsSuccessStatusCode, Is.True);
    Assert.That(session.AutoRefreshEnabled, Is.True);
}
```

### Performance Testing

```csharp
[Test]
public async Task SessionManager_WithManySessions_HandlesConcurrency()
{
    // Arrange
    var sessionManager = new SessionManager(loggerFactory);
    var httpClient = new HttpClient();
    
    var profile = new HttpClientProfile
    {
        Name = "PerformanceTest",
        Auth = new AuthSelection.None(),
        Timeout = TimeSpan.FromSeconds(30)
    };

    // Act
    var stopwatch = Stopwatch.StartNew();
    
    var tasks = new List<Task<IHttpSession>>();
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(Task.Run(() => sessionManager.Create(profile, httpClient)));
    }

    var sessions = await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert
    Assert.That(sessions.Length, Is.EqualTo(100));
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000)); // Should complete within 5 seconds

    // Cleanup
    foreach (var session in sessions)
    {
        await session.DisposeAsync();
    }
    
    await sessionManager.DisposeAsync();
}
```

## Performance Examples

### High-Throughput Scenarios

```csharp
// Optimized for high throughput
var profile = new HttpClientProfile
{
    Name = "HighThroughput",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    RateLimiter = new RateLimiterOptions
    {
        Kind = RateLimiterKind.TokenBucket,
        TokenBucket = new TokenBucketOptions
        {
            TokenBucketCapacity = 1000,
            TokenBucketRefillRate = 100,
            QueueLimit = 100
        }
    },
    Retry = new RetryOptions
    {
        MaxAttempts = 2,
        Delay = TimeSpan.FromMilliseconds(50),
        BackoffStrategy = RetryBackoff.Linear
    },
    Timeout = TimeSpan.FromSeconds(10)
};

var sessionManager = new SessionManager(loggerFactory, TimeSpan.FromMinutes(1));
var httpClient = new HttpClient();

// Create multiple sessions for parallel processing
var sessions = new List<IHttpSession>();
for (int i = 0; i < 10; i++)
{
    sessions.Add(sessionManager.Create(profile, httpClient));
}

// Process requests in parallel
var semaphore = new SemaphoreSlim(50); // Limit concurrent requests
var tasks = new List<Task>();

for (int i = 0; i < 1000; i++)
{
    tasks.Add(ProcessRequestAsync(sessions[i % sessions.Count], i, semaphore));
}

await Task.WhenAll(tasks);

async Task ProcessRequestAsync(IHttpSession session, int requestId, SemaphoreSlim semaphore)
{
    await semaphore.WaitAsync();
    try
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.example.com/data/{requestId}");
        var response = await session.ExecuteRequestAsync(request);
        
        if (requestId % 100 == 0)
        {
            Console.WriteLine($"Processed request {requestId}: {response.StatusCode}");
        }
    }
    finally
    {
        semaphore.Release();
    }
}
```

### Memory-Efficient Processing

```csharp
// Process large datasets without memory issues
var profile = new HttpClientProfile
{
    Name = "MemoryEfficient",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    Timeout = TimeSpan.FromMinutes(30)
};

await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// Stream large dataset
var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/large-dataset");
var streamed = await session.StreamResponseAsync(request);

using var reader = new StreamReader(streamed.ContentStream);
using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

var processedCount = 0;
var batch = new List<DataRecord>();

while (csvReader.Read())
{
    var record = csvReader.GetRecord<DataRecord>();
    batch.Add(record);

    if (batch.Count >= 1000)
    {
        await ProcessBatchAsync(batch);
        batch.Clear();
        processedCount += 1000;
        
        Console.WriteLine($"Processed {processedCount} records");
        
        // Force garbage collection periodically
        if (processedCount % 10000 == 0)
        {
            GC.Collect();
        }
    }
}

// Process remaining records
if (batch.Count > 0)
{
    await ProcessBatchAsync(batch);
    processedCount += batch.Count;
}

Console.WriteLine($"Total processed: {processedCount} records");
```

### Adaptive Rate Limiting

```csharp
// Use adaptive rate limiting based on machine capabilities
var profile = new HttpClientProfile
{
    Name = "AdaptiveRateLimit",
    Auth = new AuthSelection.ApiKey(apiKeyConfig),
    RateLimiter = new RateLimiterOptions
    {
        Kind = RateLimiterKind.FixedWindow
        // FixedWindow will be automatically configured by adaptive rate limiter
    },
    Timeout = TimeSpan.FromSeconds(30)
};

await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// Rate limiting automatically adapts to machine capabilities
for (int i = 0; i < 100; i++)
{
    var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.example.com/data/{i}");
    var response = await session.ExecuteRequestAsync(request);
    
    if (i % 10 == 0)
    {
        Console.WriteLine($"Request {i}: {response.StatusCode}");
    }
}
``` 