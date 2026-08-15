// Exhibit #0072: the midpoint that overflowed

// Binary search over a large numeric range - bisect [low, high] to home in on a value.
int low = 1_500_000_000;
int high = 2_000_000_000;

int mid = (low + high) / 2; // 💥 low + high overflows int before the divide

Console.WriteLine($"Range [{low}, {high}] -> midpoint {mid}");

// Self-audit: the midpoint must lie inside the range it bisects.
if (mid < low || mid > high)
{
    throw new InvalidOperationException(
        $"midpoint {mid} is outside [{low}, {high}] - low + high is {(long)low + high}, past int.MaxValue " +
        "(2147483647), so the sum wraps negative before the divide and the bisection jumps out of range");
}

Console.WriteLine("Midpoint splits the range.");
