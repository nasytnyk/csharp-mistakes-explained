// Exhibit #0072: the fix

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// An invoice total in cents: quantity x unit price. A big wholesale order overflows int.
int quantity = 50_000;
int unitPriceCents = 99_999; // 999.99 each

int totalCents;
try
{
    totalCents = checked(quantity * unitPriceCents); // throws on overflow instead of wrapping
}
catch (OverflowException)
{
    // The total does not fit in int cents - refuse it rather than bill a wrapped number.
    Console.WriteLine("Order total exceeds the int-cents limit - rejected for review.");
    Console.WriteLine("No wrong amount was billed. As it should be.");
    return;
}

Console.WriteLine($"Billing {totalCents} cents ({totalCents / 100m:0.00})");

// Self-audit: if we billed at all, it must be the real total.
long realTotalCents = (long)quantity * unitPriceCents;
if (totalCents != realTotalCents)
{
    throw new InvalidOperationException($"billed {totalCents} cents but the order is really {realTotalCents}");
}

Console.WriteLine("Billed the correct amount. As it should be.");
