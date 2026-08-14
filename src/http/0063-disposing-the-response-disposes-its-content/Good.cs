// Exhibit #0063: the fix

using System.Text;

using var client = new HttpClient(new StubApi());

// A helper that fetches a report and returns its text for the caller.
async Task<string> DownloadReportAsync(string url)
{
    using var response = await client.GetAsync(url);
    return await response.Content.ReadAsStringAsync(); // read the body fully while the response is alive
}

var report = await DownloadReportAsync("https://api.example.com/report");

Console.WriteLine($"Downloaded: {report}");

// Self-audit: the helper must return the body it fetched, not an empty or dead stream.
if (report != "report-body")
{
    throw new InvalidOperationException($"expected the report body, got '{report}'");
}

Console.WriteLine("Report downloaded. As it should be.");

// A stand-in API that returns a small text body.
class StubApi : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage { Content = new StringContent("report-body", Encoding.UTF8, "text/plain") });
}
