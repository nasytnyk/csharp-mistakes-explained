// Exhibit #0074: ToDictionary throws on a duplicate key

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// A price feed: build a SKU -> price lookup, assuming one row per SKU.
var feed = new[]
{
    (Sku: "A-1", Price: 9.99m),
    (Sku: "B-2", Price: 4.50m),
    (Sku: "A-1", Price: 8.99m), // a corrected price - a second row for the same SKU
};

Console.WriteLine($"Feed: {feed.Length} rows, {feed.Select(p => p.Sku).Distinct().Count()} distinct SKUs");

var priceBySku = feed.ToDictionary(p => p.Sku, p => p.Price); // 💥 ArgumentException: A-1 appears twice

Console.WriteLine($"A-1 price: {priceBySku["A-1"]}");

// Self-audit (unreached: ToDictionary throws on the duplicate key above).
if (priceBySku["A-1"] != 8.99m)
{
    throw new InvalidOperationException($"A-1 price is {priceBySku["A-1"]}, expected the latest 8.99");
}

Console.WriteLine("Price lookup built.");
