using Session.Contracts.Models.Wiring;

namespace Session.Contracts.Interfaces;

public interface IRequestStreamer
{
    /// <summary>
    /// Attaches a streaming body to the requests and sends it via ExecutionServie
    /// </summary>
    Task<HttpResponseMessage> StreamRequestAsync(
        HttpClientProfile profile,
        HttpRequestMessage request,
        Stream requestBody,
        CancellationToken cancellationToken = default
    );
}