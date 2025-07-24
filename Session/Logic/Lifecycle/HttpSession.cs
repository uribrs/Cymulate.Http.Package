using Authentication.Contracts.Interfaces;
using DefensiveToolkit.Contracts.Interfaces;
using Microsoft.Extensions.Logging;
using Session.Contracts.Interfaces;
using Session.Contracts.Models.Lifecycle;
using Session.Contracts.Models.Transport;
using Session.Contracts.Models.Wiring;
using Session.Logic.Transport;
using Session.Telemetry;

namespace Session.Logic.Lifecycle;

public sealed class HttpSession : IHttpSession, IAsyncDisposable
{
    public SessionId SessionId { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastUsed { get; private set; }
    public bool IsDisposed { get; private set; }

    public bool AutoRefreshEnabled => _refreshLoop is not null;

    public IAuthenticationProvider Auth { get; }
    public IDefensivePolicyRunner Policies { get; }

    private readonly HttpClient _client;
    private readonly ILogger<HttpSession>? _logger;
    private readonly object _lastUsedLock = new();
    private readonly string _authType;
    private readonly CancellationTokenSource? _refreshCts;
    private readonly Task? _refreshLoop;
    private int _refreshAttempts = 0;

    private readonly ExecuteTransportService _transportService;

    public event EventHandler<SessionId>? SessionDisposed;

    public HttpSession(
        HttpClientProfile profile,
        HttpClient client,
        IAuthenticationProvider auth,
        IDefensivePolicyRunner policies,
        ILogger<HttpSession>? logger = null,
        CancellationToken globalCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Auth = auth ?? throw new ArgumentNullException(nameof(auth));
        Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _logger = logger;

        SessionId = SessionId.New();
        CreatedAt = DateTimeOffset.UtcNow;
        LastUsed = CreatedAt;
        _authType = auth.GetType().Name;

        if (auth is IRefreshableAuthenticator refreshable && refreshable.ExpiresAt > DateTimeOffset.UtcNow)
        {
            _logger?.LogInformation("[HttpSession:{SessionId}] Starting background auth refresh loop", SessionId);
            _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(globalCancellationToken);
            _refreshLoop = StartRefreshLoop(refreshable, _refreshCts.Token);
        }
        else
        {
            _logger?.LogDebug("[HttpSession:{SessionId}] No refresh loop started (either not refreshable or no expiry set)", SessionId);
        }

        _transportService = new ExecuteTransportService(
            session: this,
            authType: _authType,
            auth: Auth,
            policies: Policies,
            client: _client,
            logger: _logger);
    }

    public Task<HttpResponseMessage> ExecuteRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        => _transportService.ExecuteRequestAsync(request, cancellationToken);

    public Task<HttpResponseMessage> StreamRequestAsync(
        HttpRequestMessage request,
        Stream requestBody,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
        => _transportService.StreamRequestAsync(request, requestBody, logger, cancellationToken);

    public Task<StreamedResponse> StreamResponseAsync(
        HttpRequestMessage request,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
        => _transportService.StreamResponseAsync(request, logger, cancellationToken);

    public void UpdateLastUsed()
    {
        var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.UpdateLastUsed);
        try
        {
            lock (_lastUsedLock)
            {
                LastUsed = DateTimeOffset.UtcNow;
            }

            SessionDiagnostics.RecordSessionInfo(activity, SessionId, CreatedAt, LastUsed);
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
            _logger?.LogDebug("[HttpSession:{SessionId}] Last used timestamp updated to {LastUsed}", SessionId, LastUsed);
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

    private Task StartRefreshLoop(IRefreshableAuthenticator refreshable, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.RefreshToken);

                try
                {
                    var now = DateTimeOffset.UtcNow;
                    var expiresAt = refreshable.ExpiresAt;
                    var timeLeft = expiresAt - now;

                    SessionDiagnostics.RecordSessionInfo(activity, SessionId, CreatedAt, LastUsed);
                    SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionAutoRefresh, true);

                    if (timeLeft <= TimeSpan.FromMinutes(1))
                    {
                        _refreshAttempts++;
                        _logger?.LogInformation("[HttpSession:{SessionId}] Token expiring in {Seconds}s — refreshing", SessionId, timeLeft.TotalSeconds);

                        await refreshable.RefreshAsync(token);

                        SessionMetrics.RecordRefreshAttempt(true, "Token expiring soon", _authType);
                        SessionDiagnostics.RecordRefreshInfo(activity, _refreshAttempts, true, "Token expiring soon");
                        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Refreshed);

                        _logger?.LogInformation("[HttpSession:{SessionId}] Token refreshed. New expiry: {ExpiresAt}", SessionId, refreshable.ExpiresAt);

                        expiresAt = refreshable.ExpiresAt;
                        timeLeft = expiresAt - DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
                    }

                    var waitTime = TimeSpan.FromSeconds(Math.Max(30, timeLeft.TotalSeconds / 2));
                    _logger?.LogDebug("[HttpSession:{SessionId}] Waiting {Seconds}s before next refresh check", SessionId, waitTime.TotalSeconds);
                    await Task.Delay(waitTime, token);
                }
                catch (OperationCanceledException)
                {
                    SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Cancelled);
                    _logger?.LogInformation("[HttpSession:{SessionId}] Refresh loop canceled", SessionId);
                    break;
                }
                catch (Exception ex)
                {
                    SessionMetrics.RecordRefreshAttempt(false, ex.Message, _authType);
                    SessionDiagnostics.RecordFailure(activity, ex);
                    SessionDiagnostics.RecordRefreshInfo(activity, _refreshAttempts, false, ex.Message);
                    SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
                    _logger?.LogWarning(ex, "[HttpSession:{SessionId}] Refresh loop error", SessionId);
                }
                finally
                {
                    activity?.Dispose();
                }
            }
        }, token);
    }

    public async ValueTask DisposeAsync()
    {
        var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.DisposeSession);

        try
        {
            if (IsDisposed)
            {
                SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Disposed);
                return;
            }

            SessionDiagnostics.RecordSessionInfo(activity, SessionId, CreatedAt, LastUsed);
            SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionAutoRefresh, AutoRefreshEnabled);
            SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.RefreshAttempts, _refreshAttempts);

            _logger?.LogInformation("[HttpSession:{SessionId}] Disposing session", SessionId);

            _refreshCts?.Cancel();
            if (_refreshLoop is not null)
                await _refreshLoop;

            _refreshCts?.Dispose();

            IsDisposed = true;

            SessionMetrics.RecordSessionDisposed((DateTimeOffset.UtcNow - CreatedAt).TotalSeconds, _authType, _refreshAttempts);

            SessionDisposed?.Invoke(this, SessionId);
            SessionDisposed = null;

            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
            _logger?.LogInformation("[HttpSession:{SessionId}] Session disposed", SessionId);
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
}