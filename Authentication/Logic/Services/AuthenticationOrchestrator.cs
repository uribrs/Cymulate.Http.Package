using Authentication.Contracts.Interfaces;
using Authentication.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace Authentication.Logic.Services;

public static class AuthenticationOrchestrator
{
    public static IAuthenticationProvider Create(AuthSelection authSelection, ILoggerFactory loggerFactory, HttpClient? httpClient = null)
    {
        return authSelection switch
        {
            AuthSelection.ApiKey(var config) => AuthProviderCollection.BuildApiKeyProvider(config, loggerFactory),
            AuthSelection.Basic(var config) => AuthProviderCollection.BuildBasicProvider(config, loggerFactory),
            AuthSelection.Jwt(var config) => AuthProviderCollection.BuildJwtProvider(config, loggerFactory),
            AuthSelection.OAuth2(var config) => AuthProviderCollection.BuildOAuth2Provider(config, loggerFactory, httpClient ?? throw new ArgumentNullException(nameof(httpClient), "HttpClient is required for OAuth2 authentication")),
            AuthSelection.Bearer(var config) => AuthProviderCollection.BuildBearerProvider(config, loggerFactory),
            AuthSelection.None => AuthProviderCollection.BuildNoAuthProvider(loggerFactory),

            _ => throw new NotSupportedException($"Authentication selection {authSelection} is not supported.")
        };
    }
}