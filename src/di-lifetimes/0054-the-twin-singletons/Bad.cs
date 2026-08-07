#:package Microsoft.Extensions.DependencyInjection@10.*
#:property PublishAot=false

// Exhibit #0054: two interfaces, two "singletons"

// One class, SettingsStore, plays two roles: IReader and IWriter. It is registered
// as a singleton under each interface - which the container reads as two singletons.

using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<IReader, SettingsStore>(); // 💥 one singleton instance for IReader...
services.AddSingleton<IWriter, SettingsStore>(); // 💥 ...a second, separate one for IWriter
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
        "the write is invisible to the reader - AddSingleton caches one instance per SERVICE type, so " +
        "registering SettingsStore under IReader and IWriter built two 'singletons', not one shared store");
}

Console.WriteLine("Reader sees the writer's value.");

interface IReader { string? Get(string key); }
interface IWriter { void Set(string key, string value); }

class SettingsStore : IReader, IWriter
{
    private readonly Dictionary<string, string> _values = new();
    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
    public void Set(string key, string value) => _values[key] = value;
}
