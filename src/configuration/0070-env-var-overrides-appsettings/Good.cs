// Exhibit #0070: the fix

#:package Microsoft.Extensions.Configuration@9.0.0
#:package Microsoft.Extensions.Configuration.Binder@9.0.0
#:package Microsoft.Extensions.Configuration.EnvironmentVariables@9.0.0
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// The forgotten override is gone: the key lives in exactly one provider now.
// (Found it with config.GetDebugView(), which names the winning source per key.)

// The developer just raised the rate limit in appsettings.json to 100.
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["App:RateLimit"] = "100" }) // appsettings.json
    .AddEnvironmentVariables()
    .Build();

int rateLimit = config.GetValue<int>("App:RateLimit");
Console.WriteLine($"App:RateLimit (effective) = {rateLimit}");

// Self-audit: the value the developer set in appsettings (100) must be the effective one.
if (rateLimit != 100)
{
    throw new InvalidOperationException(
        $"appsettings sets RateLimit=100 but the effective value is {rateLimit}");
}

Console.WriteLine("The appsettings value is in effect. As it should be.");
