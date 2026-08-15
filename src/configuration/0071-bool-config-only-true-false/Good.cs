// Exhibit #0071: the fix

#:package Microsoft.Extensions.Configuration@9.0.0
#:package Microsoft.Extensions.Configuration.Binder@9.0.0
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// A feature flag, turned on in appsettings.json the way a config bool actually parses: true.
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["Features:NewCheckout"] = "true" })
    .Build();

Console.WriteLine($"Config value: Features:NewCheckout = \"{config["Features:NewCheckout"]}\"");

bool enabled = config.GetValue<bool>("Features:NewCheckout"); // "true"/"false" are the only values bool.Parse takes

// Self-audit: the flag was turned on, so it must read as enabled.
if (!enabled)
{
    throw new InvalidOperationException("the feature flag read as false");
}

Console.WriteLine("NewCheckout is enabled. As it should be.");
