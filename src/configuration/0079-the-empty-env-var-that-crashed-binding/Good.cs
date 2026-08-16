// Exhibit #0079 - the fix: treat a blank value as "not provided", then parse.
//
// A present-but-empty env var is an empty string, not an absent key. Read the
// raw value, fall back to the default when it is blank, and only parse real text.

#:package Microsoft.Extensions.Configuration@9.*
#:package Microsoft.Extensions.Configuration.EnvironmentVariables@9.*
#:package Microsoft.Extensions.Configuration.Binder@9.*
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

const int DefaultRetries = 5;

Environment.SetEnvironmentVariable("WORKER__Retries", "");

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["Worker:Retries"] = DefaultRetries.ToString() })
    .AddEnvironmentVariables()
    .Build();

var raw = config["Worker:Retries"];
Console.WriteLine($"Worker:Retries raw value: '{raw}'");

// A blank override means "not set" - fall back; parse only when there is real text.
int retries = string.IsNullOrWhiteSpace(raw) ? DefaultRetries : int.Parse(raw);

Console.WriteLine($"Starting worker with {retries} retries. As it should be.");
