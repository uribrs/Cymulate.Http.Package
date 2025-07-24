namespace Authentication.Contracts.Models;

public sealed class JwtAuthenticationConfiguration
{
    public required string JwtToken { get; set; }

    /// <summary>
    /// Optional static expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Optional refresh hook.
    /// </summary>
    public Func<CancellationToken, Task<string>>? RefreshTokenAsync { get; set; }

    /// <summary>
    /// Optional fallback lifetime if ExpiresAt is not set. Defaults to 1 hour.
    /// </summary>
    public TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromHours(1);
}
