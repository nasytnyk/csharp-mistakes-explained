// Exhibit #0074: the fix

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// A price feed: build a SKU -> price lookup. A SKU can repeat - the latest row wins.
var feed = new[]
{
    (Sku: "A-1", Price: 9.99m),
    (Sku: "B-2", Price: 4.50m),
    (Sku: "A-1", Price: 8.99m), // a corrected price - a second row for the same SKU
};

Console.WriteLine($"Feed: {feed.Length} rows, {feed.Select(p => p.Sku).Distinct().Count()} distinct SKUs");

var priceBySku = feed.GroupBy(p => p.Sku).ToDictionary(g => g.Key, g => g.Last().Price); // last row wins per SKU

Console.WriteLine($"A-1 price: {priceBySku["A-1"]}");

// Self-audit: the latest A-1 row is 8.99, so that is the price the lookup must hold.
if (priceBySku["A-1"] != 8.99m)
{
    throw new InvalidOperationException($"A-1 price is {priceBySku["A-1"]}, expected the latest 8.99");
}

Console.WriteLine("Price lookup built. As it should be.");
