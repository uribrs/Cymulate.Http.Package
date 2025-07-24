using Authentication.Contracts.Interfaces;
using DefensiveToolkit.Contracts.Interfaces;
using DefensiveToolkit.Core;
using Microsoft.Extensions.Logging;
using Session.Contracts.Interfaces;
using Session.Contracts.Models.Transport;
using Session.Telemetry;

namespace Session.Logic.Transport;

public class ExecuteTransportService
{
    private readonly IHttpSession _session;
    private readonly ILogger? _logger;
    private readonly HttpClient _client;
    private readonly string _authType;

    public IAuthenticationProvider Auth { get; }
    public IDefensivePolicyRunner Policies { get; }

    public ExecuteTransportService(
        IHttpSession session,
        string authType,
        IAuthenticationProvider auth,
        IDefensivePolicyRunner policies,
        HttpClient client,
        ILogger logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _authType = authType ?? throw new ArgumentNullException(nameof(authType));
        Auth = auth ?? throw new ArgumentNullException(nameof(auth));
        Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HttpResponseMessage> ExecuteRequestAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        using var activity = SessionDiagnostics.StartSessionActivity(SessionTelemetryConstants.Operations.ExecuteRequest);

        try
        {
            _session.UpdateLastUsed();
            SessionDiagnostics.RecordSessionInfo(activity, _session.SessionId, _session.CreatedAt, _session.LastUsed);
            SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionId, _session.SessionId.ToString());
            SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.AuthType, _authType);

            _logger?.LogInformation("[HttpSession:{SessionId}] Authenticating request to {Uri}", _session.SessionId, request.RequestUri);
            await Auth.AuthenticateRequestAsync(request, ct);

            _logger?.LogInformation("[HttpSession:{SessionId}] Executing HTTP request to {Uri}", _session.SessionId, request.RequestUri);

            HttpResponseMessage response;
            using (RequestContextScope.With(request))
            {
                response = await Policies.ExecuteAsync(
                    token => _client.SendAsync(request, token),
                    ct
                );
            }

            SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.ResponseStatusCode, ((int)response.StatusCode).ToString());
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
            _logger?.LogInformation("[HttpSession:{SessionId}] Received {StatusCode} from {Uri}", _session.SessionId, response.StatusCode, request.RequestUri);

            return response;
        }
        catch (Exception ex)
        {
            SessionDiagnostics.RecordFailure(activity, ex);
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
            _logger?.LogError(ex, "[HttpSession:{SessionId}] Request execution failed", _session.SessionId);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    public async Task<HttpResponseMessage> StreamRequestAsync(
        HttpRequestMessage request,
        Stream requestBody,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = SessionDiagnostics.StartSessionActivity("StreamRequest");

        SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionId, _session.SessionId.ToString());
        SessionDiagnostics.SetTag(activity, "request.uri", request.RequestUri?.ToString() ?? "unknown");
        SessionDiagnostics.SetTag(activity, "request.method", request.Method.ToString());


        logger?.LogInformation("[RequestStreamer] Starting stream request to {Uri} using Session {SessionId}",
            request.RequestUri, _session.SessionId);

        try
        {
            // Analyze stream size
            var streamSize = GetStreamSizeIfPossible(requestBody);
            if (streamSize.HasValue)
            {
                SessionDiagnostics.SetTag(activity, "stream.size_bytes", streamSize.Value);
                logger?.LogDebug("[RequestStreamer] Stream size: {Size} bytes", streamSize.Value);

                if (streamSize.Value == 0)
                {
                    logger?.LogWarning("[RequestStreamer] Empty stream detected for request to {Uri}", request.RequestUri);
                }
                else if (streamSize.Value > 100 * 1024 * 1024)
                {
                    logger?.LogWarning("[RequestStreamer] Large stream detected: {Size} bytes for request to {Uri}",
                        streamSize.Value, request.RequestUri);
                }
            }
            else
            {
                SessionDiagnostics.SetTag(activity, "stream.size_bytes", "unknown");
                logger?.LogDebug("[RequestStreamer] Stream size unknown (non-seekable stream)");
            }

            // Reset stream position
            if (requestBody.CanSeek && requestBody.Position != 0)
            {
                requestBody.Position = 0;
                logger?.LogDebug("[RequestStreamer] Reset stream position to beginning");
            }

            request.Content = new StreamContent(requestBody);

            logger?.LogInformation("[RequestStreamer] Executing stream request");
            var response = await ExecuteRequestAsync(request, cancellationToken);

            SessionDiagnostics.SetTag(activity, "response.status_code", ((int)response.StatusCode).ToString());
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);

            logger?.LogInformation("[RequestStreamer] Stream request completed successfully with status {StatusCode}",
                response.StatusCode);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Cancelled);
            logger?.LogWarning("[RequestStreamer] Stream request to {Uri} was cancelled", request.RequestUri);
            throw;
        }
        catch (Exception ex)
        {
            SessionDiagnostics.RecordFailure(activity, ex);
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
            logger?.LogError(ex, "[RequestStreamer] Failed to stream request to {Uri}", request.RequestUri);
            throw;
        }
    }

    public async Task<StreamedResponse> StreamResponseAsync(
        HttpRequestMessage request,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = SessionDiagnostics.StartSessionActivity("StreamResponse");

        SessionDiagnostics.SetTag(activity, SessionTelemetryConstants.Tags.SessionId, _session.SessionId.ToString());
        SessionDiagnostics.SetTag(activity, "request.uri", request.RequestUri?.ToString() ?? "unknown");
        SessionDiagnostics.SetTag(activity, "request.method", request.Method.ToString());

        logger?.LogInformation("[ResponseStreamer] Starting response stream for {Uri} (Session {SessionId})",
            request.RequestUri, _session.SessionId);

        try
        {
            logger?.LogDebug("[ResponseStreamer] Executing request via HttpSession");
            var response = await ExecuteRequestAsync(request, cancellationToken);

            SessionDiagnostics.SetTag(activity, "response.status_code", ((int)response.StatusCode).ToString());

            var contentLength = response.Content.Headers.ContentLength;
            var contentType = response.Content.Headers.ContentType?.ToString();

            if (contentLength.HasValue)
            {
                SessionDiagnostics.SetTag(activity, "response.content_length", contentLength.Value);
                logger?.LogDebug("[ResponseStreamer] Response content length: {ContentLength} bytes", contentLength.Value);

                if (contentLength.Value == 0)
                {
                    logger?.LogWarning("[ResponseStreamer] Empty response content from {Uri}", request.RequestUri);
                }
                else if (contentLength.Value > 100 * 1024 * 1024)
                {
                    logger?.LogWarning("[ResponseStreamer] Large response detected: {ContentLength} bytes from {Uri}",
                        contentLength.Value, request.RequestUri);
                }
            }
            else
            {
                SessionDiagnostics.SetTag(activity, "response.content_length", "unknown");
                logger?.LogDebug("[ResponseStreamer] Response content length unknown (chunked or no Content-Length header)");
            }

            if (!string.IsNullOrEmpty(contentType))
            {
                SessionDiagnostics.SetTag(activity, "response.content_type", contentType);
                logger?.LogDebug("[ResponseStreamer] Response content type: {ContentType}", contentType);
            }

            logger?.LogInformation("[ResponseStreamer] Reading response content as stream");
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Success);
            logger?.LogInformation("[ResponseStreamer] Response stream ready for {Uri} with status {StatusCode}",
                request.RequestUri, response.StatusCode);

            return new StreamedResponse
            {
                RawResponse = response,
                ContentStream = stream
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Cancelled);
            logger?.LogWarning("[ResponseStreamer] Response streaming for {Uri} was cancelled", request.RequestUri);
            throw;
        }
        catch (Exception ex)
        {
            SessionDiagnostics.RecordFailure(activity, ex);
            SessionDiagnostics.RecordOutcome(activity, SessionTelemetryConstants.Outcomes.Failure);
            logger?.LogError(ex, "[ResponseStreamer] Failed to stream response from {Uri}", request.RequestUri);
            throw;
        }
    }

    private static long? GetStreamSizeIfPossible(Stream stream)
    {
        if (!stream.CanSeek) return null;

        try { return stream.Length; }
        catch { return null; }
    }
}