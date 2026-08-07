// Exhibit #0050: the widening that came too late

// A retention window - "keep audit records for N days" - computed in milliseconds.
// `long` is chosen deliberately, for headroom. But the arithmetic runs in `int`
// and overflows before it is ever widened to `long`. (The N comes from config, so
// the product is not a compile-time constant - which is the only reason it reaches
// runtime instead of failing to compile.)

int retentionDays = 30; // from config

long retentionMs = retentionDays * 24 * 60 * 60 * 1000; // 💥 int * int math wraps, THEN widens to long

Console.WriteLine($"Retention window: {retentionMs} ms ({retentionMs / 86_400_000.0:F1} days).");

if (retentionMs <= 0)
{
    throw new InvalidOperationException(
        $"a 30-day retention computed to {retentionMs} ms - the int multiplication overflowed past " +
        "int.MaxValue before it was widened to long, so the long never held the true value");
}

Console.WriteLine("Retention window is positive.");
