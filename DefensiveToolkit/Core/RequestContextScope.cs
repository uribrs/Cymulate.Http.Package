namespace DefensiveToolkit.Core;

public static class RequestContextScope
{
    private static readonly AsyncLocal<HttpRequestMessage> _current = new();
    public static HttpRequestMessage? Current => _current.Value;
    public static IDisposable With(HttpRequestMessage request)
    {
        var original = _current.Value!;
        _current.Value = request;
        return new DisposableAction(() => _current.Value = original);
    }
}