// Exhibit #0064: the fix

#:property PublishAot=false

using System.Net.Http.Headers;
using System.Text.Json;

// Fetch an order and read it as JSON. The API content-negotiates: it returns JSON only when
// asked for it, and its default is XML.
using var client = new HttpClient(new NegotiatingApi());
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); // ask for JSON

var body = await client.GetStringAsync("https://api.example.com/orders/1");
Console.WriteLine($"Body received: {body}");

var order = JsonSerializer.Deserialize<Order>(body);
Console.WriteLine($"Parsed total: {order?.Total}");

// Self-audit: order 1 has total 9.99; the parse must yield it, not fail or default.
if (order?.Total != 9.99m)
{
    throw new InvalidOperationException(
        $"expected total 9.99, got {(order?.Total)?.ToString() ?? "null"}");
}

Console.WriteLine("Order parsed. As it should be.");

record Order(int Id, decimal Total);

// A stand-in API that content-negotiates: JSON when the caller accepts it, XML otherwise.
class NegotiatingApi : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        bool wantsJson = request.Headers.Accept.Any(a => a.MediaType == "application/json");
        var body = wantsJson ? """{"Id":1,"Total":9.99}""" : "<order><id>1</id><total>9.99</total></order>";
        return Task.FromResult(new HttpResponseMessage { Content = new StringContent(body) });
    }
}
