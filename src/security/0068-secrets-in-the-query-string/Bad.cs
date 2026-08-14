// Exhibit #0068: a secret in the query string ends up in the logs

using System.Net.Http.Headers;

// The server's access log - stands in for the web server, proxies, and CDNs that log request lines.
var accessLog = new List<string>();
using var client = new HttpClient(new LoggingServer(accessLog)) { BaseAddress = new Uri("https://api.example.com/") };

const string apiKey = "sk_live_9f8b2c1d";

// Pass the API key as a query-string parameter.
using var request = new HttpRequestMessage(HttpMethod.Get, $"reports/monthly?api_key={apiKey}"); // 💥 secret in the URL
await client.SendAsync(request);

Console.WriteLine($"Access log line: {accessLog[^1]}");

// Self-audit: the server's access log (method + URL) must not contain the secret.
if (accessLog[^1].Contains(apiKey))
{
    throw new InvalidOperationException(
        $"the access log recorded the API key '{apiKey}' - a secret in the query string is written verbatim to " +
        "server logs, proxy logs, and browser history (and sent as Referer), in plaintext, even over HTTPS");
}

Console.WriteLine("No secret in the access log.");

// A stand-in server that logs the request line, the way real servers, proxies and CDNs do.
class LoggingServer(List<string> log) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        log.Add($"{request.Method} {request.RequestUri}"); // method + full URL (path + query), not headers
        return Task.FromResult(new HttpResponseMessage());
    }
}
