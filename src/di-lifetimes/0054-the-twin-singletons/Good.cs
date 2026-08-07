#:package Microsoft.Extensions.DependencyInjection@10.*
#:property PublishAot=false

// Exhibit #0054: the fix

// The same class in two roles - but registered as the concrete type ONCE, with each
// interface forwarding to that single registration, so all three resolve one instance.

using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<SettingsStore>();
services.AddSingleton<IReader>(sp => sp.GetRequiredService<SettingsStore>()); // forward to the one instance
services.AddSingleton<IWriter>(sp => sp.GetRequiredService<SettingsStore>()); // forward to the one instance
var provider = services.BuildServiceProvider();

var writer = provider.GetRequiredService<IWriter>();
var reader = provider.GetRequiredService<IReader>();

writer.Set("theme", "dark");
string? readBack = reader.Get("theme");

Console.WriteLine($"Wrote theme=dark via IWriter; read via IReader: {readBack ?? "<missing>"}");
Console.WriteLine($"Reader and writer are the same instance? {ReferenceEquals(reader, writer)}");

// Self-audit: a value written through the store must be readable from the store.
if (readBack != "dark")
{
    throw new InvalidOperationException(
        "the write is invisible to the reader");
}

Console.WriteLine("Reader sees the writer's value. As it should be.");

interface IReader { string? Get(string key); }
interface IWriter { void Set(string key, string value); }

class SettingsStore : IReader, IWriter
{
    private readonly Dictionary<string, string> _values = new();
    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
    public void Set(string key, string value) => _values[key] = value;
}
