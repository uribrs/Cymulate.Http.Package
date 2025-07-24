namespace Authentication.Contracts.Interfaces;

public interface IAuthenticationProvider : IDisposable
{
    Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}