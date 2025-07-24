namespace Authentication.Contracts.Models;

public abstract record AuthSelection
{
    public sealed record ApiKey(ApiKeyAuthenticationConfiguration Config) : AuthSelection;
    public sealed record Basic(BasicAuthenticationConfiguration Config) : AuthSelection;
    public sealed record Jwt(JwtAuthenticationConfiguration Config) : AuthSelection;
    public sealed record OAuth2(OAuth2AuthenticationConfiguration Config) : AuthSelection;
    public sealed record Bearer(BearerAuthenticationConfiguration Config) : AuthSelection;
    public sealed record None : AuthSelection;
}