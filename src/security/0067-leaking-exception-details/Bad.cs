// Exhibit #0067: returning exception details to the client

// An API endpoint reads a config file and, on failure, "helpfully" returns the error to the caller.
string HandleRequest()
{
    try
    {
        return File.ReadAllText("/srv/app/config/db-secret.conf"); // absent in this environment
    }
    catch (Exception ex)
    {
        return $"Error: {ex.Message}"; // 💥 the exception message goes straight to the client
    }
}

string clientResponse = HandleRequest();
Console.WriteLine($"Response to client: {clientResponse}");

// Self-audit: the client response must not expose internal filesystem paths.
if (clientResponse.Contains("/srv/app/config"))
{
    throw new InvalidOperationException(
        "the client response leaked the internal path '/srv/app/config/...' - returning ex.Message exposes the " +
        "filesystem layout (and, for other exceptions, SQL, server names, stack traces) to the caller");
}

Console.WriteLine("Client response carries no internal details.");
