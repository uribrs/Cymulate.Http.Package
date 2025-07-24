namespace Session.Contracts.Models.Wiring;

public sealed class HttpClientSecurityOptions
{
    public bool IgnoreServerCertificateErrors { get; init; } = false;
}