// Exhibit #0081 - the fix: one key, once. The merge is resolved so each setting
// appears a single time; the file loads and the value binds. The load-and-read
// code is identical to Bad.cs.

#:package Microsoft.Extensions.Configuration@9.*
#:package Microsoft.Extensions.Configuration.Json@9.*
#:package Microsoft.Extensions.Configuration.Binder@9.*
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// appsettings.json with the merge resolved: "MaxConnections" appears once.
var path = Path.Combine(Path.GetTempPath(), $"cme0081-{Guid.NewGuid():N}.json");
File.WriteAllText(path, """
    {
      "Database": {
        "MaxConnections": 100,
        "CommandTimeout": 30
      }
    }
    """);

var config = new ConfigurationBuilder()
    .AddJsonFile(path)
    .Build();

int maxConnections = config.GetValue<int>("Database:MaxConnections");
Console.WriteLine($"Max connections: {maxConnections}");
Console.WriteLine("Configuration loaded. As it should be.");
