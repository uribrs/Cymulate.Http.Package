using Authentication.Contracts.Interfaces;
using Authentication.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Web;

namespace Authentication.Logic.Strategies;

public sealed class ApiKeyAuthentication : IRequestAuthenticator
{
    private readonly ApiKeyAuthenticationConfiguration _config;
    private readonly ILogger<ApiKeyAuthentication> _logger;

    public ApiKeyAuthentication(
        ApiKeyAuthenticationConfiguration config,
        ILogger<ApiKeyAuthentication> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (!_config.IsEnabled)
        {
            _logger.LogDebug("API key authentication is disabled. Skipping.");
            return Task.CompletedTask;
        }

        try
        {
            _logger.LogDebug("Applying API key auth to {RequestUri}", request.RequestUri);

            if (_config.UseQueryParameter)
            {
                if (string.IsNullOrWhiteSpace(_config.QueryParameterName) || string.IsNullOrWhiteSpace(_config.ApiKey))
                    throw new InvalidOperationException("QueryParameterName and ApiKey must be set for query parameter authentication");

                ApplyQueryParam(request, _config.QueryParameterName, _config.ApiKey);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_config.HeaderName) || string.IsNullOrWhiteSpace(_config.ApiKey))
                    throw new InvalidOperationException("HeaderName and ApiKey must be set for header authentication");

                ApplyHeader(request, _config.HeaderName, _config.ApiKey, _config.HeaderPrefix);
            }

            _logger.LogDebug("API key auth applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply API key auth");
            throw;
        }

        return Task.CompletedTask;
    }

    private void ApplyHeader(HttpRequestMessage request, string name, string value, string? prefix)
    {
        string headerValue;

        if (_config.CombineAsAccessSecretPair)
        {
            if (string.IsNullOrWhiteSpace(_config.ApiKey) || string.IsNullOrWhiteSpace(_config.SecretKey))
                throw new InvalidOperationException("Access and Secret keys must be set when CombineAsAccessSecretPair is true");

            headerValue = $"accessKey={_config.ApiKey}; secretKey={_config.SecretKey}";
        }
        else
        {
            headerValue = string.IsNullOrWhiteSpace(prefix)
                ? value
                : $"{prefix} {value}";
        }

        request.Headers.Add(name, headerValue);
        _logger.LogDebug("API key applied via header {HeaderName}", name);
    }

    private void ApplyQueryParam(HttpRequestMessage request, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(_config.QueryParameterName))
            throw new InvalidOperationException("Query parameter name is required for query-based API key auth");

        var uri = request.RequestUri!;
        var builder = new UriBuilder(uri);
        var query = HttpUtility.ParseQueryString(builder.Query);
        query[name] = value;
        builder.Query = query.ToString();
        request.RequestUri = builder.Uri;

        _logger.LogDebug("API key applied via query param {QueryParameterName}", name);
    }
}