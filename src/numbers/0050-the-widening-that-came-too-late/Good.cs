// Exhibit #0050: the fix

// The same retention window - but one operand is widened BEFORE the multiplication,
// so the whole expression runs in `long` from the first operator on and never
// overflows `int`.

int retentionDays = 30; // from config

long retentionMs = (long)retentionDays * 24 * 60 * 60 * 1000; // widen first: long * int stays long

Console.WriteLine($"Retention window: {retentionMs} ms ({retentionMs / 86_400_000.0:F1} days).");

if (retentionMs <= 0)
{
    throw new InvalidOperationException(
        $"a 30-day retention computed to {retentionMs} ms");
}

Console.WriteLine("Retention window is positive. As it should be.");
