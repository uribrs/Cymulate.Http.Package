namespace Session.Contracts.Models.Transport;

public sealed class StreamedResponse
{
    public required HttpResponseMessage RawResponse { get; init; }
    public required Stream ContentStream { get; init; }
    public long? ContentLength => RawResponse.Content.Headers.ContentLength;
    public bool IsSuccessStatusCode => RawResponse.IsSuccessStatusCode;
}