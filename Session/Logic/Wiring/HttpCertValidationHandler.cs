using Microsoft.Extensions.Logging;
using Session.Contracts.Models.Wiring;

namespace Session.Logic.Wiring;

public static class HttpCertValidationHandler
{
    public static HttpClientHandler Create(HttpClientSecurityOptions security, ILogger logger)
    {
        var handler = new HttpClientHandler();

        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            if (security.IgnoreServerCertificateErrors)
            {
                logger.LogWarning("[HttpCertValidationHandler] Ignoring server certificate errors for {Url}", message.RequestUri);
                return true; // Ignore all certificate errors
            }
            if (errors != System.Net.Security.SslPolicyErrors.None)
            {
                logger.LogError("[HttpCertValidationHandler] Certificate error for {Url}: {Errors}",
                    message.RequestUri, errors);
            }

            return errors == System.Net.Security.SslPolicyErrors.None;
        };

        return handler;
    }
}