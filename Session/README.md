# Cymulate.Http.Session

A high-level, **transparent HTTP transport layer** that combines authentication, resilience policies and rich telemetry under a single `IHttpSession` interface.  Use it when you want to send HTTP requests without having to think about tokens, retries, rate-limiters or circuit-breakers.

---

## Package

```
<PackageReference Include="Cymulate.Http.Package.Session" Version="1.*" />
```

Session depends transitively on:

* `Cymulate.Http.Package.Authentication`
* `Cymulate.Http.Package.DefensiveToolkit`

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| **HttpSession** | Encapsulates an `HttpClient`, an authentication provider and a defensive‐policy runner.  Exposes convenience methods to execute, stream request bodies and stream response bodies.  Implements `IAsyncDisposable`. |
| **SessionBuilder** | Factory that wires up `HttpSession` given a `HttpClientProfile`, an `HttpClient` and a `LoggerFactory`. |
| **SessionManager** | Optional orchestrator that keeps track of multiple sessions, performs periodic cleanup and exposes look-up helpers (LRU, idle sessions, etc.). |
| **Telemetry** | All operations create `Activity` spans and structured logs.  Instrumentation can be exported via OpenTelemetry. |

---

## Quick-start

### 1. Build a profile

```csharp
var apiKeyCfg = new ApiKeyAuthenticationConfiguration
{
    IsEnabled  = true,
    HeaderName = "X-API-Key",
    ApiKey     = Environment.GetEnvironmentVariable("MY_API_KEY")!
};

AuthSelection auth = new AuthSelection.ApiKey(apiKeyCfg);

var retry = new RetryOptions
{
    MaxAttempts    = 3,
    Delay          = TimeSpan.FromMilliseconds(300),
    BackoffStrategy = RetryBackoff.Exponential
};

var profile = new HttpClientProfile
{
    Name    = "DemoProfile",
    Auth    = auth,
    Retry   = retry,
    Timeout = TimeSpan.FromSeconds(20)
};
```

### 2. Obtain an `HttpClient`

```csharp
// Preferred in ASP.NET Core / DI-friendly apps
var httpClient = httpClientFactory.CreateClient("CymulateApi");

// Or construct manually in a console / test scenario
// using var httpClient = new HttpClient();
```

### 3. Create a session and execute a request

```csharp
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

var request  = new HttpRequestMessage(HttpMethod.Get, "https://postman-echo.com/get");
var response = await session.ExecuteRequestAsync(request, cts.Token);

Console.WriteLine($"Status: {response.StatusCode}");
```

> The session automatically authenticates, applies rate-limiting, retries, circuit-breaking and logs everything.

### 4. Managing many sessions

```csharp
var mgr = new SessionManager(loggerFactory);

// create & track
var sess = mgr.Create(profile, httpClient);

// lookup later (updates LastUsed automatically)
var same = mgr.Get(sess.SessionId);

// remove manually (optional – automatic when sess.DisposeAsync is called)
mgr.Remove(sess.SessionId);

await mgr.DisposeAsync();
```

---

## Streaming helpers

```csharp
await using var session = SessionBuilder.Build(profile, httpClient, loggerFactory);

// 1. Stream request body
await using var file = File.OpenRead("large.zip");
var uploadReq  = new HttpRequestMessage(HttpMethod.Post, "/upload");
var uploadResp = await session.StreamRequestAsync(uploadReq, file);

// 2. Stream response body
var downloadReq = new HttpRequestMessage(HttpMethod.Get, "/big-file");
var streamed    = await session.StreamResponseAsync(downloadReq);
await using var target = File.Create("download.zip");
await streamed.ContentStream.CopyToAsync(target);
```

Both helpers analyse stream size, add relevant telemetry tags and warn on empty / oversized data.

---

## Cancellation & Disposal

* All public APIs accept a `CancellationToken`.
* `HttpSession` starts a background refresh loop only when the chosen authenticator supports it.  Cancelling the token passed to `SessionBuilder.Build` propagates to that loop.
* Always dispose the session **before** disposing the underlying `HttpClient`.

```csharp
await session.DisposeAsync();
httpClient.Dispose();
```

Using `await using` / `using` statements (as shown) is the simplest way to guarantee correct order.

---

## Telemetry integration

The project emits `Activity` spans under three sources:

* `Authentication`
* `DefensivePolicyRunner`
* `Session`

Add them to your OpenTelemetry pipeline:

```csharp
Sdk.CreateTracerProviderBuilder()
   .AddSource("Authentication", "DefensivePolicyRunner", "Session")
   .AddHttpClientInstrumentation()
   .AddJaegerExporter()        // or OTLP / Zipkin / Console
   .Build();
```

---

## Advanced tips

* **Custom per-request rate limiter** – attach a custom policy via `HttpRequestMessage.Options[RequestOptionKeys.CustomRateLimiterKey]`.
* **Circuit-breaker tuning** – supply `CircuitBreakerOptions` in `HttpClientProfile`.
* **Integration testing** – pass a mocked `HttpClient` (e.g., `RichardSzalay.MockHttp`) to assert behaviour without hitting the network.

---

## License

&copy; Cymulate — MIT-licensed.  See repository root for full license text. 