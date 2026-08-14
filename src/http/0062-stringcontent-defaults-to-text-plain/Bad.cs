// Exhibit #0062: StringContent defaults to text/plain, not application/json

using System.Net;
using System.Text;

// POST an order as JSON to an API that requires application/json (it returns 415 otherwise).
using var client = new HttpClient(new JsonOnlyApi()) { BaseAddress = new Uri("https://api.example.com/") };

var json = """{"sku":"A-100","qty":3}""";
var content = new StringContent(json); // 💥 no media type -> Content-Type: text/plain; charset=utf-8

var response = await client.PostAsync("orders", content);

Console.WriteLine($"Sent Content-Type: {content.Headers.ContentType}");
Console.WriteLine($"Response: {(int)response.StatusCode} {response.StatusCode}");

// Self-audit: the order must be created, not rejected for its media type.
if (response.StatusCode != HttpStatusCode.Created)
{
    throw new InvalidOperationException(
        $"the API rejected the POST with {(int)response.StatusCode} {response.StatusCode} - new StringContent(json) " +
        "sends Content-Type text/plain, not application/json, so the JSON body is refused before anyone reads it");
}

Console.WriteLine("Order created.");

// A stand-in for a real JSON API: it accepts only application/json.
class JsonOnlyApi : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var mediaType = request.Content?.Headers.ContentType?.MediaType;
        var status = mediaType == "application/json" ? HttpStatusCode.Created : HttpStatusCode.UnsupportedMediaType;
        return Task.FromResult(new HttpResponseMessage(status));
    }
}
