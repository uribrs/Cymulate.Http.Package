using Authentication.Contracts.Interfaces;
using Authentication.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace Authentication.Logic.Strategies;

public sealed class JwtAuthentication : IRefreshableAuthenticator
{
    private readonly JwtAuthenticationConfiguration _config;
    private readonly ILogger<JwtAuthentication> _logger;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private DateTimeOffset _expiresAt;

    public DateTimeOffset ExpiresAt => _expiresAt;

    public JwtAuthentication(JwtAuthenticationConfiguration config, ILogger<JwtAuthentication> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _expiresAt = config.ExpiresAt ?? DateTimeOffset.UtcNow.Add(config.DefaultLifetime);
    }

    public Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.JwtToken);
        _logger.LogDebug("Applied JWT Bearer authentication to request {RequestUri}", request.RequestUri);
        return Task.CompletedTask;
    }

    public Task<bool> IsValidAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var valid = _expiresAt > now.AddMinutes(1);

        _logger.LogDebug(_config.ExpiresAt is null
            ? "No expiration configured, fallback expiry is {ExpiryTime}"
            : "Configured expiration is {ExpiryTime}", _expiresAt);

        return Task.FromResult(valid);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_config.RefreshTokenAsync is null)
        {
            _logger.LogWarning("JWT token refresh requested, but no delegate was configured.");
            return;
        }

        await _refreshSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check: another thread might have refreshed while we were waiting
            var currentExpiry = _expiresAt; // Use the internal field which gets updated after refresh
            if (currentExpiry > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                _logger.LogDebug("Token already refreshed by another thread, skipping");
                return;
            }

            _logger.LogDebug("Refreshing JWT token...");
            var newToken = await _config.RefreshTokenAsync.Invoke(cancellationToken);

            _config.JwtToken = newToken;
            _expiresAt = DateTimeOffset.UtcNow.Add(_config.DefaultLifetime);

            _logger.LogDebug("JWT token refreshed successfully. New expiry: {ExpiryTime}", _expiresAt);
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }
}