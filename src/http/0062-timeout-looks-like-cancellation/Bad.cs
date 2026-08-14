// Exhibit #0062: a timeout and a user-cancel are the same exception type

// A resilience wrapper: on a server timeout we should retry; on a user cancel we should not.
// The server hangs, so HttpClient.Timeout (200 ms) fires - a transient timeout that must retry.
using var client = new HttpClient(new NeverRespondsHandler()) { Timeout = TimeSpan.FromMilliseconds(200) };

var userToken = new CancellationTokenSource().Token; // the user did NOT cancel anything

string decision;
try
{
    await client.GetAsync("https://api.example.com/orders", userToken);
    decision = "ok";
}
catch (OperationCanceledException)
{
    // classify by exception type alone
    decision = "user cancelled - do not retry"; // 💥 a HttpClient.Timeout throws this same type
}

Console.WriteLine($"Decision: {decision}");

// Self-audit: the server timed out (the user never cancelled), so this must be a retryable timeout.
if (decision != "timeout - retry")
{
    throw new InvalidOperationException(
        $"classified a server timeout as '{decision}' - HttpClient.Timeout throws the same " +
        "OperationCanceledException a user cancel does, so catching it as a cancel drops the retry the timeout needed");
}

Console.WriteLine("Timeout classified as retryable.");

class NeverRespondsHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken); // never completes until cancelled or timed out
        return new HttpResponseMessage();
    }
}
