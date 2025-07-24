using Authentication.Contracts.Interfaces;
using Authentication.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace Authentication.Logic.Strategies;

public sealed class BearerAuthentication : IRequestAuthenticator
{
    private readonly BearerAuthenticationConfiguration _config;
    private readonly ILogger<BearerAuthentication> _logger;

    public BearerAuthentication(BearerAuthenticationConfiguration config, ILogger<BearerAuthentication> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (!_config.IsEnabled)
        {
            _logger.LogDebug("Bearer authentication is disabled");
            return Task.CompletedTask;
        }

        _logger.LogDebug("Applying bearer token to request");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AccessToken);
        return Task.CompletedTask;
    }
}