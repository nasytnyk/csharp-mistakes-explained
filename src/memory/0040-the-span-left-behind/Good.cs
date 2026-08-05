// Exhibit #0040: the fix

using System.Runtime.InteropServices;

// The same end-of-round scoring - but we let the late player register first, and
// only then take the span, so it views the List's final buffer.

var scores = new List<int>(4) { 10, 20, 30, 40 }; // four players, capacity 4 (full)

scores.Add(50); // the fifth player registers first, while no span is aliasing the buffer

Span<int> live = CollectionsMarshal.AsSpan(scores); // taken after the last size change

// Apply the +100 end-of-round bonus in place, through the span.
for (int i = 0; i < live.Length; i++)
    live[i] += 100;

Console.WriteLine($"Bonus applied. First player's score via the list: {scores[0]}.");

if (scores[0] != 110)
{
    throw new InvalidOperationException(
        $"applied a +100 bonus through the span, but the list shows {scores[0]}, not 110");
}

Console.WriteLine("Every player got the bonus. As it should be.");
