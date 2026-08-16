// Exhibit #0081: a duplicate key in appsettings.json crashes config load.
//
// A merge left the same key in the file twice. It is not "last wins" - the JSON
// provider refuses to load the file at all, and the app dies at startup with a
// message that names the file, not the duplicated key.

#:package Microsoft.Extensions.Configuration@9.*
#:package Microsoft.Extensions.Configuration.Json@9.*
#:package Microsoft.Extensions.Configuration.Binder@9.*
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

// appsettings.json after a bad merge: "MaxConnections" survived twice.
var path = Path.Combine(Path.GetTempPath(), $"cme0081-{Guid.NewGuid():N}.json");
File.WriteAllText(path, """
    {
      "Database": {
        "MaxConnections": 10,
        "CommandTimeout": 30,
        "MaxConnections": 100
      }
    }
    """);

var config = new ConfigurationBuilder()
    .AddJsonFile(path) // 💥 InvalidDataException at Build: "Failed to load configuration from file '...'"
    .Build();

int maxConnections = config.GetValue<int>("Database:MaxConnections");
Console.WriteLine($"Max connections: {maxConnections}");
