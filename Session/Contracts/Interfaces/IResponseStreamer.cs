using Session.Contracts.Models.Transport;
using Session.Contracts.Models.Wiring;

namespace Session.Contracts.Interfaces;

public interface IResponseStreamer
{
    Task<StreamedResponse> StreamResponseAsync(
       HttpClientProfile profile,
       HttpRequestMessage request,
       CancellationToken cancellationToken = default
    );
}