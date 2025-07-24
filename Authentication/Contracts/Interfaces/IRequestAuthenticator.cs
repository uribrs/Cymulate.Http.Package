namespace Authentication.Contracts.Interfaces;

public interface IRequestAuthenticator
{
    Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}