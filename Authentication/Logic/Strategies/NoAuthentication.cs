using Authentication.Contracts.Interfaces;

namespace Authentication.Logic.Strategies;

public sealed class NoAuthentication : IRequestAuthenticator
{
    public Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        // Intentionally does nothing
        return Task.CompletedTask;
    }
}