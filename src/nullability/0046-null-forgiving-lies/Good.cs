// Exhibit #0046: the fix

// The same startup - but the absent key is handled with a real fallback instead
// of a `!` that just promises it away.

var config = new Dictionary<string, string?>
{
    ["App:Version"] = "4.2.0",
    // "App:DisplayName" was never set in this environment
};

string displayName = config.GetValueOrDefault("App:DisplayName") ?? "My App"; // handle the null, don't assert it away

string banner = displayName.ToUpperInvariant();

Console.WriteLine($"Starting {banner} v{config.GetValueOrDefault("App:Version")}...");
Console.WriteLine("Started with the fallback name. As it should be.");
