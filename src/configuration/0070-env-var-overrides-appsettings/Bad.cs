// Exhibit #0070: an environment variable silently overrides appsettings

#:package Microsoft.Extensions.Configuration@9.0.0
#:package Microsoft.Extensions.Configuration.Binder@9.0.0
#:package Microsoft.Extensions.Configuration.EnvironmentVariables@9.0.0
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// A deployment set this env var long ago (a container, CI, launchSettings) and everyone forgot it.
Environment.SetEnvironmentVariable("App__RateLimit", "10");

// The developer just raised the rate limit in appsettings.json to 100.
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["App:RateLimit"] = "100" }) // appsettings.json
    .AddEnvironmentVariables()                                                            // 💥 added after -> wins
    .Build();

int rateLimit = config.GetValue<int>("App:RateLimit");
Console.WriteLine($"App:RateLimit (effective) = {rateLimit}");

// Self-audit: the value the developer set in appsettings (100) must be the effective one.
if (rateLimit != 100)
{
    throw new InvalidOperationException(
        $"appsettings sets RateLimit=100 but the effective value is {rateLimit} - the env var App__RateLimit sits " +
        "in a later provider, and later providers win, so the appsettings change did nothing (no error, no log)");
}

Console.WriteLine("The appsettings value is in effect.");
