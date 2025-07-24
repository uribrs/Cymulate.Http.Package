using Authentication.Contracts.Interfaces;
using Authentication.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;

namespace Authentication.Logic.Strategies;

public sealed class BasicAuthentication : IRequestAuthenticator
{
    private readonly BasicAuthenticationConfiguration _config;
    private readonly ILogger<BasicAuthentication> _logger;

    public BasicAuthentication(BasicAuthenticationConfiguration config, ILogger<BasicAuthentication> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_config.Username) || string.IsNullOrWhiteSpace(_config.Password))
        {
            throw new ArgumentException("Username and Password must not be null or whitespace for Basic authentication.");
        }
    }

    public Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (_config.IgnoreIfAlreadyAuthenticated && request.Headers.Authorization is not null)
        {
            _logger.LogDebug("Authorization header already set. Skipping Basic auth due to config.");
            return Task.CompletedTask;
        }

        try
        {
            var credentials = $"{_config.Username}:{_config.Password}";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64);

            _logger.LogDebug("Applied Basic authentication to request {RequestUri}",
                request.RequestUri?.ToString() ?? "(null URI)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply Basic authentication to request");
            throw;
        }

        return Task.CompletedTask;
    }
}