// Exhibit #0072: integer overflow wraps silently

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// An invoice total in cents: quantity x unit price. A big wholesale order overflows int.
int quantity = 50_000;
int unitPriceCents = 99_999; // 999.99 each

int totalCents = quantity * unitPriceCents; // 💥 4,999,950,000 overflows int and wraps to 704,982,704

Console.WriteLine($"Billing {totalCents} cents ({totalCents / 100m:0.00})");

// Self-audit: the amount billed must be the real total, not a wrapped one.
long realTotalCents = (long)quantity * unitPriceCents;
if (totalCents != realTotalCents)
{
    throw new InvalidOperationException(
        $"billed {totalCents} cents but the order is really {realTotalCents} - quantity * unitPriceCents " +
        "overflowed int and wrapped silently, so the customer is charged a fraction of what they owe, with no error");
}

Console.WriteLine("Billed the correct amount.");
