namespace Authentication.Contracts.Interfaces;

public interface IRefreshableAuthenticator : IRequestAuthenticator
{
    /// <summary>
    /// Refreshes authentication credentials if needed.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the current authentication is still valid.
    /// </summary>
    Task<bool> IsValidAsync(CancellationToken cancellationToken = default);

    DateTimeOffset ExpiresAt { get; }
}