// Exhibit #0087 - the fix: expose an immutable array; a tweak builds a copy.
//
// ImmutableArray has no in-place setter, so `Config.RetryDelays[0] = 30` will not
// even compile; SetItem returns a new array and the shared table stays put.

using System.Collections.Immutable;

int[] defaults = { 1, 2, 4 };

// Request A builds its OWN adjusted schedule; the shared table cannot be mutated in place.
ImmutableArray<int> burst = Config.RetryDelays.SetItem(0, 30); // returns a new array
Console.WriteLine($"Request A uses: [{string.Join(", ", burst)}]");

// Request B, unrelated, reads the same shared array a moment later.
Console.WriteLine($"Request B reads retry delays: [{string.Join(", ", Config.RetryDelays)}]");

if (!Config.RetryDelays.SequenceEqual(defaults))
    throw new InvalidOperationException(
        $"the shared defaults are now [{string.Join(", ", Config.RetryDelays)}]");

Console.WriteLine("Shared defaults intact. As it should be.");

static class Config
{
    public static readonly ImmutableArray<int> RetryDelays = ImmutableArray.Create(1, 2, 4); // truly immutable defaults
}
