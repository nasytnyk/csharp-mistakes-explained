// Exhibit #0080: an array from an environment variable needs indexed keys.
//
// An outbound-call allowlist is configured from the environment. The deploy
// provides it as one comma-separated variable - and the array binder returns
// nothing, because a comma is not a separator.

#:package Microsoft.Extensions.Configuration@9.*
#:package Microsoft.Extensions.Configuration.EnvironmentVariables@9.*
#:package Microsoft.Extensions.Configuration.Binder@9.*
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// The deploy sets the allowlist as a single comma-joined value.
Environment.SetEnvironmentVariable("ALLOWEDHOSTS", "payments.acme.com,billing.acme.com,mail.acme.com");

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

string[] allowed = config.GetSection("AllowedHosts").Get<string[]>() ?? []; // 💥 comes back empty

Console.WriteLine($"Allowed hosts loaded: {allowed.Length}");
foreach (var host in allowed)
    Console.WriteLine($"  - {host}");

// Self-audit: we configured three allowed hosts.
if (allowed.Length != 3)
    throw new InvalidOperationException(
        $"configured 3 allowed hosts but binding produced {allowed.Length}: the environment variable holds one " +
        "comma-joined string, and the array binder needs indexed keys (ALLOWEDHOSTS__0, __1, __2) - the comma " +
        "is not a separator, so the allowlist came back empty and every outbound call would be blocked");

Console.WriteLine("Allowlist ready.");
