// Exhibit #0063: disposing the response disposes its content

using System.Text;

using var client = new HttpClient(new StubApi());

// A helper that fetches a report and returns its text for the caller.
async Task<string> DownloadReportAsync(string url)
{
    HttpContent content;
    using (var response = await client.GetAsync(url)) // response - and its Content - disposed here
        content = response.Content;

    return await content.ReadAsStringAsync(); // 💥 the content was disposed with the response
}

var report = await DownloadReportAsync("https://api.example.com/report");

Console.WriteLine($"Downloaded: {report}");

// Self-audit: the helper must return the body it fetched, not a disposed content object.
if (report != "report-body")
{
    throw new InvalidOperationException($"expected the report body, got '{report}'");
}

Console.WriteLine("Report downloaded.");

// A stand-in API that returns a small text body.
class StubApi : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage { Content = new StringContent("report-body", Encoding.UTF8, "text/plain") });
}
