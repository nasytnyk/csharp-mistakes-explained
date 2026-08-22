// Exhibit #0087: `readonly` on an array field freezes the reference, not the elements.
//
// Config.RetryDelays is a `static readonly int[]` of shared defaults. One request
// "tweaks" the first delay for a burst, trusting readonly to keep the shared copy
// safe - and mutates the single array that every request reads.

int[] defaults = { 1, 2, 4 };

// Request A bumps the first retry delay for a burst - readonly "protects" the table... or so it looks.
Config.RetryDelays[0] = 30; // 💥 readonly guards the reference; the elements are still writable

// Request B, unrelated, reads the same shared array a moment later.
Console.WriteLine($"Request B reads retry delays: [{string.Join(", ", Config.RetryDelays)}]");

// Self-audit: the shared defaults must be unchanged for everyone else.
if (!Config.RetryDelays.SequenceEqual(defaults))
    throw new InvalidOperationException(
        $"the shared defaults are now [{string.Join(", ", Config.RetryDelays)}]: `readonly` on an array field " +
        "freezes only the reference, so RetryDelays[0]=30 mutated the one array every caller shares - Request B " +
        "now backs off 30s because Request A edited what it thought was a private copy");

Console.WriteLine("Shared defaults intact.");

static class Config
{
    public static readonly int[] RetryDelays = { 1, 2, 4 }; // "immutable" defaults (seconds)
}
