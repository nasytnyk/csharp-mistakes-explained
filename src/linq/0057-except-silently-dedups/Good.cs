// Exhibit #0057: the fix

// The same pick list - but we FILTER out the out-of-stock products instead of taking a set
// difference, so every in-stock unit (duplicates included) survives.
string[] pickList = { "widget", "widget", "gadget", "gizmo" }; // two widgets, one gadget, one gizmo
string[] outOfStock = { "gizmo" };

string[] toPull = pickList.Where(x => !outOfStock.Contains(x)).ToArray(); // filter, not set difference

Console.WriteLine($"Pick list ({pickList.Length}): {string.Join(", ", pickList)}");
Console.WriteLine($"Out of stock: {string.Join(", ", outOfStock)}");
Console.WriteLine($"To pull ({toPull.Length}): {string.Join(", ", toPull)}");

// Self-audit: we removed 1 out-of-stock unit from 4, so 3 units must remain (two widgets).
if (toPull.Length != 3)
{
    throw new InvalidOperationException(
        $"expected 3 units to pull, got {toPull.Length}");
}

Console.WriteLine("Every in-stock unit is on the pull list. As it should be.");
