using Authentication.Contracts.Interfaces;
using DefensiveToolkit.Contracts.Interfaces;
using Microsoft.Extensions.Logging;
using Session.Contracts.Models.Lifecycle;
using Session.Contracts.Models.Transport;

namespace Session.Contracts.Interfaces;

public interface IHttpSession : IAsyncDisposable
{
    SessionId SessionId { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset LastUsed { get; }
    IAuthenticationProvider Auth { get; }
    IDefensivePolicyRunner Policies { get; }
    bool IsDisposed { get; }
    bool AutoRefreshEnabled { get; }

    void UpdateLastUsed();

    Task<HttpResponseMessage> ExecuteRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> StreamRequestAsync(
        HttpRequestMessage request,
        Stream requestBody,
        ILogger? logger = null,
        CancellationToken cancellationToken = default);

    Task<StreamedResponse> StreamResponseAsync(
        HttpRequestMessage request,
        ILogger? logger = null,
        CancellationToken cancellationToken = default);
}
