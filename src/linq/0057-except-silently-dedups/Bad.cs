// Exhibit #0057: Except silently dedups

// A warehouse pick list: the units to pull. A product appears once per unit, so "widget"
// twice means pull two widgets. We drop the out-of-stock products from the list.
string[] pickList = { "widget", "widget", "gadget", "gizmo" }; // two widgets, one gadget, one gizmo
string[] outOfStock = { "gizmo" };

string[] toPull = pickList.Except(outOfStock).ToArray(); // 💥 Except returns a SET (distinct)

Console.WriteLine($"Pick list ({pickList.Length}): {string.Join(", ", pickList)}");
Console.WriteLine($"Out of stock: {string.Join(", ", outOfStock)}");
Console.WriteLine($"To pull ({toPull.Length}): {string.Join(", ", toPull)}");

// Self-audit: we removed 1 out-of-stock unit from 4, so 3 units must remain (two widgets).
if (toPull.Length != 3)
{
    throw new InvalidOperationException(
        $"expected 3 units to pull, got {toPull.Length} - Except returns a set, so it dropped the out-of-stock " +
        "gizmo AND collapsed the two widgets into one; the second widget is silently never pulled");
}

Console.WriteLine("Every in-stock unit is on the pull list.");
