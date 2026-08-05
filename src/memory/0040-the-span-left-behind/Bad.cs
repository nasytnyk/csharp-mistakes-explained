// Exhibit #0040: a Span over a List that grew

using System.Runtime.InteropServices;

// End-of-round scoring. We take a fast in-place span over the scores to add a
// bonus to everyone at once - but a late player registers first, and the list
// quietly moves to a bigger buffer underneath the span.

var scores = new List<int>(4) { 10, 20, 30, 40 }; // four players, capacity 4 (full)

Span<int> live = CollectionsMarshal.AsSpan(scores); // fast view over the backing array

scores.Add(50); // 💥 a fifth player registers - the List reallocates; `live` now aliases the OLD array

// Apply the +100 end-of-round bonus in place, through the span.
for (int i = 0; i < live.Length; i++)
    live[i] += 100;

Console.WriteLine($"Bonus applied. First player's score via the list: {scores[0]}.");

if (scores[0] != 110)
{
    throw new InvalidOperationException(
        $"applied a +100 bonus through the span, but the list shows {scores[0]}, not 110 - the span still points at the buffer the List abandoned when it grew");
}

Console.WriteLine("Every player got the bonus.");
