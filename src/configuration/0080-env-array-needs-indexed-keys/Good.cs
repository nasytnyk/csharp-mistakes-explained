// Exhibit #0080 - the fix: give each array element its own indexed key.
//
// Configuration represents an array as KEY:0, KEY:1, KEY:2 - and for env vars
// the `__` separator spells that as KEY__0, KEY__1, KEY__2. The binding code is
// identical to Bad.cs; only how the environment expresses the array changes.

#:package Microsoft.Extensions.Configuration@9.*
#:package Microsoft.Extensions.Configuration.EnvironmentVariables@9.*
#:package Microsoft.Extensions.Configuration.Binder@9.*
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// The deploy sets one indexed variable per allowlist entry.
Environment.SetEnvironmentVariable("ALLOWEDHOSTS__0", "payments.acme.com");
Environment.SetEnvironmentVariable("ALLOWEDHOSTS__1", "billing.acme.com");
Environment.SetEnvironmentVariable("ALLOWEDHOSTS__2", "mail.acme.com");

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

string[] allowed = config.GetSection("AllowedHosts").Get<string[]>() ?? [];

Console.WriteLine($"Allowed hosts loaded: {allowed.Length}");
foreach (var host in allowed)
    Console.WriteLine($"  - {host}");

if (allowed.Length != 3)
    throw new InvalidOperationException(
        $"configured 3 allowed hosts but binding produced {allowed.Length}");

Console.WriteLine("Allowlist ready. As it should be.");
