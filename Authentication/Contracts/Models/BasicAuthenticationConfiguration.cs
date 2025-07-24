namespace Authentication.Contracts.Models;

public sealed class BasicAuthenticationConfiguration
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public bool IgnoreIfAlreadyAuthenticated { get; init; } = true;

}