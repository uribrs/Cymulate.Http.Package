using Authentication.Contracts.Interfaces;
using Microsoft.Extensions.Logging;

namespace Authentication.Logic.Services;

public sealed class AuthenticationProvider : IAuthenticationProvider, IRefreshableAuthenticator, IDisposable
{
    private readonly IRequestAuthenticator _authenticator;
    private readonly IRefreshableAuthenticator? _refreshable;
    private readonly ILogger<AuthenticationProvider> _logger;

    public DateTimeOffset ExpiresAt => _refreshable?.ExpiresAt ?? DateTimeOffset.MinValue;

    public AuthenticationProvider(
        IRequestAuthenticator authenticator,
        ILogger<AuthenticationProvider> logger)
    {
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _refreshable = authenticator as IRefreshableAuthenticator;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Authenticating request to {RequestUri}", request.RequestUri);

        if (_refreshable is not null)
        {
            var isValid = await _refreshable.IsValidAsync(cancellationToken);
            if (!isValid)
            {
                _logger.LogDebug("Token is invalid or near expiry. Refreshing...");
                await _refreshable.RefreshAsync(cancellationToken);
            }
        }
        else
        {
            _logger.LogDebug("Authenticator does not support validation/refresh.");
        }

        await _authenticator.AuthenticateRequestAsync(request, cancellationToken);

        _logger.LogDebug("Authentication complete.");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_refreshable is null)
        {
            _logger.LogDebug("Authenticator does not support refresh.");
            return;
        }

        _logger.LogDebug("Refreshing credentials explicitly...");
        await _refreshable.RefreshAsync(cancellationToken);
    }

    public async Task<bool> IsValidAsync(CancellationToken cancellationToken = default)
    {
        if (_refreshable is null)
        {
            _logger.LogDebug("Authenticator does not support validation.");
            return true;
        }

        return await _refreshable.IsValidAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_authenticator is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}