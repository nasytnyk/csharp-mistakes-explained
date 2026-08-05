// Exhibit #0039: stackalloc inside a loop

// A nightly job formats 200,000 report rows. Each row needs a small scratch
// buffer, so someone "optimized" new byte[1024] into a stackalloc to skip the
// per-row heap allocation. It runs clean on a page of test data and dies on the
// batch.

Console.WriteLine("Formatting 200000 rows...");

long checksum = 0;
for (int row = 0; row < 200_000; row++)
{
    Span<byte> scratch = stackalloc byte[1024]; // 💥 freed at method exit, not each iteration
    scratch[row % scratch.Length] = (byte)row;  // format the row into the scratch buffer
    checksum += scratch[row % scratch.Length];
}

// Never reached: the stack overflows thousands of rows before the last one. The
// exact row depends on the stack size, but overflow well before 200,000 is
// certain - each iteration leaks another 1 KB that only a method return frees.
Console.WriteLine($"All 200000 rows formatted. Checksum {checksum}.");
