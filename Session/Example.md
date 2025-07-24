// Program.cs
using System.Net.Http;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

using Authentication.Contracts.Models;
using DefensiveToolkit.Contracts.Options;
using DefensiveToolkit.Contracts.Enums;
using Session.Contracts.Models.Wiring;
using Session.Logic.Lifecycle;  // SessionManager
using Session.Logic.Wiring;     // SessionBuilder helpers

// ────────────────────────────────
// 1.  Logging + tracing boilerplate
// ────────────────────────────────
using var loggerFactory = LoggerFactory.Create(b =>
{
    b.SetMinimumLevel(LogLevel.Debug)
     .AddSimpleConsole(o =>
     {
         o.TimestampFormat = "HH:mm:ss ";
         o.SingleLine = true;
     });
});

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("DefensivePolicyRunner")
    .AddSource("Session")
    .AddSource("Authentication")
    .AddHttpClientInstrumentation()
    .AddConsoleExporter()
    .Build();

var log = loggerFactory.CreateLogger("Demo");

// ────────────────────────────────
// 2.  CancellationToken for whole demo
// ────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ────────────────────────────────
// 3.  Build profile with API-key auth
// ────────────────────────────────
var apiKeyCfg = new ApiKeyAuthenticationConfiguration
{
    IsEnabled = true,
    HeaderName = "X-API-Key",
    ApiKey = "<YOUR_API_KEY>"
};

AuthSelection auth = new AuthSelection.ApiKey(apiKeyCfg);

var retry = new RetryOptions
{
    MaxAttempts = 3,
    Delay = TimeSpan.FromMilliseconds(200),
    BackoffStrategy = RetryBackoff.Exponential,
    ShouldRetry = (ex, _) => ex is HttpRequestException
};

var profile = new HttpClientProfile
{
    Name = "DemoProfile",
    Auth = auth,
    Retry = retry,
    Timeout = TimeSpan.FromSeconds(20)
};

// ────────────────────────────────
// 4.  Build HttpClient externally
// ────────────────────────────────
using var httpClient = new HttpClient();

// ────────────────────────────────
// 5.  Use SessionManager
// ────────────────────────────────
var sessionManager = new SessionManager(loggerFactory);

// -- create session
var session = sessionManager.Create(profile, httpClient, cts.Token);
log.LogInformation("Session {Id} created", session.SessionId);

// -- execute request
var request = new HttpRequestMessage(HttpMethod.Get, "https://postman-echo.com/get");
var response = await session.ExecuteRequestAsync(request, cts.Token);
log.LogInformation("Status from first call: {Status}", response.StatusCode);

// -- retrieve same session later
var sameSession = sessionManager.Get(session.SessionId);
log.LogInformation("Retrieved last-used: {LastUsed}", sameSession?.LastUsed);

// -- optional: remove session manually
bool removed = sessionManager.Remove(session.SessionId);
log.LogInformation("Removed? {Removed}", removed);

// -- or rely on automatic disposal / cleanup
await session.DisposeAsync();                // fires SessionDisposed event
await sessionManager.DisposeAsync();         // cleans up timer + remaining sessions

log.LogInformation("Finished — press <Enter> or Ctrl+C to exit.");
Console.ReadLine();