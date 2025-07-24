namespace Authentication.Contracts.Models;

public sealed class OAuth2AuthenticationConfiguration
{
    public required string TokenEndpoint { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public string? Scope { get; init; }
    public string? CurrentAccessToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}