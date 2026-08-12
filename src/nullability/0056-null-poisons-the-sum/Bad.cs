// Exhibit #0056: one null poisons the whole sum

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Summing invoice line amounts with a running total. Most lines have an amount; one
// optional line has not been priced yet - its amount is null.
decimal?[] lineAmounts = { 10.00m, 5.50m, null, 20.00m };

decimal? total = 0m;
foreach (var amount in lineAmounts)
    total += amount; // 💥 total + null is null - and null + anything stays null

Console.WriteLine($"Lines: {string.Join(", ", lineAmounts.Select(a => a?.ToString() ?? "null"))}");
Console.WriteLine($"Total: {(total.HasValue ? total.Value.ToString() : "null")}");

// Self-audit: the priced lines total 35.50; a single unpriced line must not erase it.
if (total != 35.50m)
{
    throw new InvalidOperationException(
        $"total came out {(total.HasValue ? total.Value.ToString() : "null")} instead of 35.50 - nullable " +
        "arithmetic propagates null, so one null line turned the entire running total into null");
}

Console.WriteLine("Total reflects every priced line.");
