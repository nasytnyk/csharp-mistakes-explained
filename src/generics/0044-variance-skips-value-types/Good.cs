// Exhibit #0044: the fix

using System.Collections; // the non-generic IEnumerable

// The same exporter - but it probes with the NON-generic IEnumerable, which every
// List<T> implements, so value-type collections take the enumeration path too.

static int RowsFor(object payload)
{
    if (payload is IEnumerable items)        // non-generic: List<int> implements it
        return items.Cast<object>().Count(); // materialize each element as object
    return 1;                                // treat it as a single scalar value
}

var tags   = new List<string> { "alpha", "beta", "gamma" }; // 3 text tags
var scores = new List<int>    { 10, 20, 30 };               // 3 numeric scores

int tagRows   = RowsFor(tags);
int scoreRows = RowsFor(scores);

Console.WriteLine($"tags (List<string>): {tagRows} rows; scores (List<int>): {scoreRows} rows.");

if (scoreRows != 3)
{
    throw new InvalidOperationException(
        $"exported {scoreRows} row for a 3-item List<int>");
}

Console.WriteLine("Both collections exported every item. As it should be.");
