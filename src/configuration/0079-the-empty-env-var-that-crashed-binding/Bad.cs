// Exhibit #0079: a present-but-empty environment variable crashes typed binding.
//
// appsettings ships a safe default (Worker:Retries = 5). A deployment sets the
// override env var to a BLANK value - and startup dies converting "" to int.

#:package Microsoft.Extensions.Configuration@9.*
#:package Microsoft.Extensions.Configuration.EnvironmentVariables@9.*
#:package Microsoft.Extensions.Configuration.Binder@9.*
#:property PublishAot=false

using Microsoft.Extensions.Configuration;

const int DefaultRetries = 5;

// A CI step / container manifest sets the override, but leaves the value blank.
Environment.SetEnvironmentVariable("WORKER__Retries", "");

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["Worker:Retries"] = DefaultRetries.ToString() })
    .AddEnvironmentVariables() // the blank env var wins over the appsettings default
    .Build();

Console.WriteLine($"Worker:Retries raw value: '{config["Worker:Retries"]}'"); // '' - present but empty

int retries = config.GetValue<int>("Worker:Retries"); // 💥 InvalidOperationException: cannot convert '' to Int32

Console.WriteLine($"Starting worker with {retries} retries.");
