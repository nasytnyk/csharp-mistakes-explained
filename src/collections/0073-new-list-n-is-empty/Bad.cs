// Exhibit #0073: new List<T>(n) is empty - n is capacity, not count

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Twelve monthly totals, "pre-sized" to 12 slots - or so it looks.
var monthly = new List<decimal>(12); // 💥 12 is the capacity; Count is 0 - there are no slots

Console.WriteLine($"Slots ready: Count = {monthly.Count} (Capacity {monthly.Capacity})");

var sales = new[] { (Month: 0, Amount: 100m), (Month: 5, Amount: 200m) };
foreach (var s in sales)
    monthly[s.Month] += s.Amount; // no monthly[0] exists -> ArgumentOutOfRangeException

Console.WriteLine($"January total: {monthly[0]}");

// Self-audit (unreached: the indexer throws on the empty list above).
if (monthly[0] != 100m)
{
    throw new InvalidOperationException($"January total is {monthly[0]}, expected 100");
}

Console.WriteLine("Monthly totals recorded.");
