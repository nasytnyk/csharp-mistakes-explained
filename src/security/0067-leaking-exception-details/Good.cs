// Exhibit #0067: the fix

// An API endpoint reads a config file and, on failure, logs the detail and returns a safe message.
string HandleRequest()
{
    try
    {
        return File.ReadAllText("/srv/app/config/db-secret.conf"); // absent in this environment
    }
    catch (Exception ex)
    {
        string traceId = "req-8f3a";
        Console.Error.WriteLine($"[server log] {traceId}: {ex}"); // full detail stays server-side
        return $"Something went wrong. Reference: {traceId}";     // generic message for the client
    }
}

string clientResponse = HandleRequest();
Console.WriteLine($"Response to client: {clientResponse}");

// Self-audit: the client response must not expose internal filesystem paths.
if (clientResponse.Contains("/srv/app/config"))
{
    throw new InvalidOperationException("the client response leaked an internal path");
}

Console.WriteLine("Client response carries no internal details. As it should be.");
