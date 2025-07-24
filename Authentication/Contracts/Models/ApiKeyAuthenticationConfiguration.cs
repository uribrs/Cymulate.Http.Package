namespace Authentication.Contracts.Models;

public sealed class ApiKeyAuthenticationConfiguration
{
    public string? ApiKey { get; init; }
    public string? SecretKey { get; init; }

    public string? HeaderName { get; init; }
    public string? HeaderPrefix { get; init; }

    public bool UseQueryParameter { get; init; } = false;
    public string? QueryParameterName { get; init; }

    public bool IsEnabled { get; init; } = true;
    public bool CombineAsAccessSecretPair { get; init; } = false;
}
