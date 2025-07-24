namespace Authentication.Contracts.Models;

public sealed class BearerAuthenticationConfiguration
{
    public required string AccessToken { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string StrategyType => "Bearer";
}
