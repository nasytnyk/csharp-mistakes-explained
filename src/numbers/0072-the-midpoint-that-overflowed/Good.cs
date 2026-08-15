// Exhibit #0072: the fix

// Binary search over a large numeric range - bisect [low, high] to home in on a value.
int low = 1_500_000_000;
int high = 2_000_000_000;

int mid = low + (high - low) / 2; // high - low fits in int, so no overflow

Console.WriteLine($"Range [{low}, {high}] -> midpoint {mid}");

// Self-audit: the midpoint must lie inside the range it bisects.
if (mid < low || mid > high)
{
    throw new InvalidOperationException($"midpoint {mid} is outside [{low}, {high}]");
}

Console.WriteLine("Midpoint splits the range. As it should be.");
