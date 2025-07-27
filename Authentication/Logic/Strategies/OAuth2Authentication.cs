using Authentication.Contracts.Interfaces;
using Authentication.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Authentication.Logic.Strategies;

public sealed class OAuth2Authentication : IRefreshableAuthenticator
{
    private readonly OAuth2AuthenticationConfiguration _config;
    private readonly ILogger<OAuth2Authentication> _logger;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    public DateTimeOffset ExpiresAt => _config.ExpiresAt ?? DateTimeOffset.MinValue;

    public OAuth2Authentication(
        OAuth2AuthenticationConfiguration config,
        HttpClient http,
        ILogger<OAuth2Authentication> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _http = http;
    }

    public async Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.CurrentAccessToken))
        {
            _logger.LogDebug("No token available, requesting initial token");
            await RefreshAsync(cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.CurrentAccessToken);
        _logger.LogDebug("Applied OAuth2 Bearer token to request {RequestUri}", request.RequestUri);
    }

    public Task<bool> IsValidAsync(CancellationToken cancellationToken = default)
    {
        if (_config.ExpiresAt is null)
        {
            _logger.LogDebug("No expiration info available; assuming token is valid.");
            return Task.FromResult(true);
        }

        var isValid = _config.ExpiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1);
        _logger.LogDebug("OAuth2 token is {State}", isValid ? "valid" : "expired or expiring soon");
        return Task.FromResult(isValid);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check: another thread might have refreshed while we were waiting
            if (!string.IsNullOrWhiteSpace(_config.CurrentAccessToken) &&
                _config.ExpiresAt.HasValue &&
                _config.ExpiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                _logger.LogDebug("Token already refreshed by another thread, skipping");
                return;
            }

            _logger.LogDebug("Requesting new OAuth2 token from {Endpoint}", _config.TokenEndpoint);

            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _config.ClientId,
                ["client_secret"] = _config.ClientSecret
            };

            if (!string.IsNullOrWhiteSpace(_config.Scope))
                body["scope"] = _config.Scope;

            using var response = await _http.PostAsync(
                _config.TokenEndpoint,
                new FormUrlEncodedContent(body),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to retrieve OAuth2 token. Status: {Status}", response.StatusCode);
                throw new InvalidOperationException($"OAuth2 token request failed: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenProp) ||
                tokenProp.GetString() is not string token)
            {
                throw new InvalidOperationException("Invalid OAuth2 response: missing access_token");
            }

            _config.CurrentAccessToken = token;
            var expiresIn = root.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;
            _config.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            _logger.LogDebug("OAuth2 token refreshed. Expires at {ExpiresAt}", _config.ExpiresAt);
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }
}