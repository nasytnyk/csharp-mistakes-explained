// Exhibit #0068: the fix

using System.Net.Http.Headers;

// The server's access log - stands in for the web server, proxies, and CDNs that log request lines.
var accessLog = new List<string>();
using var client = new HttpClient(new LoggingServer(accessLog)) { BaseAddress = new Uri("https://api.example.com/") };

const string apiKey = "sk_live_9f8b2c1d";

// Pass the API key in the Authorization header - out of the URL, out of the logs.
using var request = new HttpRequestMessage(HttpMethod.Get, "reports/monthly");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
await client.SendAsync(request);

Console.WriteLine($"Access log line: {accessLog[^1]}");

// Self-audit: the server's access log (method + URL) must not contain the secret.
if (accessLog[^1].Contains(apiKey))
{
    throw new InvalidOperationException($"the access log recorded the API key '{apiKey}'");
}

Console.WriteLine("No secret in the access log. As it should be.");

// A stand-in server that logs the request line, the way real servers, proxies and CDNs do.
class LoggingServer(List<string> log) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        log.Add($"{request.Method} {request.RequestUri}"); // method + full URL (path + query), not headers
        return Task.FromResult(new HttpResponseMessage());
    }
}
