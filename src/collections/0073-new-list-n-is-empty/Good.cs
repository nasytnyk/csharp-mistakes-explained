// Exhibit #0073: the fix

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Twelve monthly totals - twelve real, zero-valued slots to index from the start.
var monthly = Enumerable.Repeat(0m, 12).ToList(); // 12 elements, all 0 (not just capacity)

Console.WriteLine($"Slots ready: Count = {monthly.Count} (Capacity {monthly.Capacity})");

var sales = new[] { (Month: 0, Amount: 100m), (Month: 5, Amount: 200m) };
foreach (var s in sales)
    monthly[s.Month] += s.Amount; // monthly[0] exists now

Console.WriteLine($"January total: {monthly[0]}");

// Self-audit: January (month 0) took a 100 sale, so its total must be 100.
if (monthly[0] != 100m)
{
    throw new InvalidOperationException($"January total is {monthly[0]}, expected 100");
}

Console.WriteLine("Monthly totals recorded. As it should be.");
