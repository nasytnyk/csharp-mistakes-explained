// Exhibit #0055: the fix

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Two systems report the same unit price: the catalog stores one decimal place, the
// invoice two. Numerically it is the same money.
decimal catalogPrice = 1.5m;
decimal invoicePrice = 1.50m;

Console.WriteLine($"catalog: {catalogPrice}   invoice: {invoicePrice}   equal: {catalogPrice == invoicePrice}");

// Reconciliation records each distinct price it has seen - keyed by the decimal value.
var pricesSeen = new HashSet<decimal>();
pricesSeen.Add(catalogPrice);
pricesSeen.Add(invoicePrice); // decimal equality ignores scale, so the equal price collapses

Console.WriteLine($"Distinct prices seen: {pricesSeen.Count} ({string.Join(", ", pricesSeen)})");

// Self-audit: the two prices are equal, so reconciliation must see exactly ONE price.
if (pricesSeen.Count != 1)
{
    throw new InvalidOperationException(
        $"two equal prices ({catalogPrice} == {invoicePrice}) were recorded as {pricesSeen.Count} distinct values");
}

Console.WriteLine("Reconciliation saw one price. As it should be.");
