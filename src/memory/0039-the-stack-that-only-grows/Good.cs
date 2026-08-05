// Exhibit #0039: the fix

// The same nightly job - but the scratch buffer is allocated once, above the
// loop, and reused for every row. One stackalloc, no per-iteration growth.

Console.WriteLine("Formatting 200000 rows...");

Span<byte> scratch = stackalloc byte[1024]; // one buffer, hoisted out of the loop

long checksum = 0;
for (int row = 0; row < 200_000; row++)
{
    scratch[row % scratch.Length] = (byte)row;  // format the row into the scratch buffer
    checksum += scratch[row % scratch.Length];
}

// Reached: the stack holds one 1 KB buffer the whole time, not one per row.
Console.WriteLine($"All 200000 rows formatted. Checksum {checksum}. As it should be.");
