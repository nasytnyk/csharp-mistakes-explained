// Exhibit #0046: the null-forgiving operator lies

// Reading a display name from config. GetValueOrDefault returns string? because
// the key may be absent - but "we always set it in this environment", so the
// warning is paid off with a `!`. This whole file builds with zero warnings.

var config = new Dictionary<string, string?>
{
    ["App:Version"] = "4.2.0",
    // "App:DisplayName" was never set in this environment
};

string displayName = config.GetValueOrDefault("App:DisplayName")!; // 💥 `!` silences the warning; the value is null

// The flow analysis now believes displayName is non-null, so every use below is
// warning-free too - the one `!` vouched for all of them.
string banner = displayName.ToUpperInvariant();                    // NRE here

Console.WriteLine($"Starting {banner} v{config.GetValueOrDefault("App:Version")}...");
Console.WriteLine("Started.");
