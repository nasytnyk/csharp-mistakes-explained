// Exhibit #0055: decimal keeps its scale

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Two systems report the same unit price: the catalog stores one decimal place, the
// invoice two. Numerically it is the same money.
decimal catalogPrice = 1.5m;
decimal invoicePrice = 1.50m;

Console.WriteLine($"catalog: {catalogPrice}   invoice: {invoicePrice}   equal: {catalogPrice == invoicePrice}");

// Reconciliation records each distinct price it has seen - keyed by its displayed text.
var pricesSeen = new HashSet<string>();
pricesSeen.Add(catalogPrice.ToString()); // 💥 "1.5"
pricesSeen.Add(invoicePrice.ToString()); //    "1.50" - a different string for an equal value

Console.WriteLine($"Distinct prices seen: {pricesSeen.Count} ({string.Join(", ", pricesSeen)})");

// Self-audit: the two prices are equal, so reconciliation must see exactly ONE price.
if (pricesSeen.Count != 1)
{
    throw new InvalidOperationException(
        $"two equal prices ({catalogPrice} == {invoicePrice}) were recorded as {pricesSeen.Count} distinct values - " +
        "decimal keeps its scale, so 1.5m and 1.50m are equal numbers but format to different text");
}

Console.WriteLine("Reconciliation saw one price.");
