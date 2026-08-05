// Exhibit #0043: the fix

// The same settings bag - but the read unboxes to int?, which accepts the null a
// missing value became, so a real int comes through and an absent one defaults.

int? configuredRetries = LoadSetting("retries"); // not configured -> null

var settings = new Dictionary<string, object?>();
settings["retries"] = configuredRetries;          // boxing an EMPTY int? stores a plain null

Console.WriteLine($"Stored an int? setting. The bag now holds null: {settings["retries"] is null}.");

// Unbox to int?: null stays empty, a real int comes through - then default it.
int retries = (int?)settings["retries"] ?? 3;

Console.WriteLine($"Retrying up to {retries} times.");

if (retries != 3)
{
    throw new InvalidOperationException($"expected the default of 3 retries, got {retries}");
}

Console.WriteLine("Missing setting fell back to the default. As it should be.");

static int? LoadSetting(string key) => null;      // "retries" is not configured
