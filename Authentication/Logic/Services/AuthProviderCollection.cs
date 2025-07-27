using Authentication.Contracts.Interfaces;
using Authentication.Contracts.Models;
using Authentication.Logic.Strategies;
using Authentication.Logic.Validation;
using Microsoft.Extensions.Logging;

namespace Authentication.Logic.Services;

public static class AuthProviderCollection
{
    public static IAuthenticationProvider BuildApiKeyProvider(
        ApiKeyAuthenticationConfiguration config,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<ApiKeyAuthentication>();
        var strategy = new ApiKeyAuthentication(Validate.NotNull(config), logger);

        var providerLogger = loggerFactory.CreateLogger<AuthenticationProvider>();
        return new AuthenticationProvider(strategy, providerLogger);
    }

    public static IAuthenticationProvider BuildBasicProvider(
        BasicAuthenticationConfiguration config,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<BasicAuthentication>();
        var strategy = new BasicAuthentication(Validate.NotNull(config), logger);

        var providerLogger = loggerFactory.CreateLogger<AuthenticationProvider>();
        return new AuthenticationProvider(strategy, providerLogger);
    }

    public static IAuthenticationProvider BuildOAuth2Provider(
        OAuth2AuthenticationConfiguration config,
        ILoggerFactory loggerFactory,
        HttpClient httpClient)
    {
        var logger = loggerFactory.CreateLogger<OAuth2Authentication>();
        var strategy = new OAuth2Authentication(Validate.NotNull(config), httpClient, logger);

        var providerLogger = loggerFactory.CreateLogger<AuthenticationProvider>();
        return new AuthenticationProvider(strategy, providerLogger);
    }

    public static IAuthenticationProvider BuildJwtProvider(
        JwtAuthenticationConfiguration config,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<JwtAuthentication>();
        var strategy = new JwtAuthentication(Validate.NotNull(config), logger);

        var providerLogger = loggerFactory.CreateLogger<AuthenticationProvider>();
        return new AuthenticationProvider(strategy, providerLogger);
    }

    public static IAuthenticationProvider BuildBearerProvider(
        BearerAuthenticationConfiguration config,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<BearerAuthentication>();
        var strategy = new BearerAuthentication(Validate.NotNull(config), logger);

        var providerLogger = loggerFactory.CreateLogger<AuthenticationProvider>();
        return new AuthenticationProvider(strategy, providerLogger);
    }

    public static IAuthenticationProvider BuildNoAuthProvider(ILoggerFactory loggerFactory)
    {
        var providerLogger = loggerFactory.CreateLogger<AuthenticationProvider>();
        return new AuthenticationProvider(new NoAuthentication(), providerLogger);
    }
}