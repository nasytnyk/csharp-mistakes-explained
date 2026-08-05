// Exhibit #0043: boxing an empty Nullable<T>

// A settings bag stores values as object. We drop an optional retry count (int?)
// into it and read it back as int, sure that "a value type can't be null".

int? configuredRetries = LoadSetting("retries"); // not configured -> null

var settings = new Dictionary<string, object?>();
settings["retries"] = configuredRetries;          // boxing an EMPTY int? stores a plain null

Console.WriteLine($"Stored an int? setting. The bag now holds null: {settings["retries"] is null}.");

// ...far away, another component reads the setting back and uses it...
int retries = (int)settings["retries"]!;          // 💥 NullReferenceException: the box is null, not an int

Console.WriteLine($"Retrying up to {retries} times.");

static int? LoadSetting(string key) => null;      // "retries" is not configured
