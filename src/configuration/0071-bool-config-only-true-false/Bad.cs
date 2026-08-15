// Exhibit #0071: a config bool accepts only true/false

#:package Microsoft.Extensions.Configuration@9.0.0
#:package Microsoft.Extensions.Configuration.Binder@9.0.0
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// A feature flag, turned on in appsettings.json the way most config systems expect: 1.
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["Features:NewCheckout"] = "1" })
    .Build();

Console.WriteLine($"Config value: Features:NewCheckout = \"{config["Features:NewCheckout"]}\"");

bool enabled = config.GetValue<bool>("Features:NewCheckout"); // 💥 "1" is not true/false - throws

// Self-audit (never reached: GetValue<bool> throws on "1" above).
if (!enabled)
{
    throw new InvalidOperationException("the feature flag read as false");
}

Console.WriteLine("NewCheckout is enabled.");
