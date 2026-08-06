// Exhibit #0044: covariance skips value types

// An audit exporter flattens each payload: if the value is a sequence, it writes
// one row per item; otherwise one row for the whole value. It probes the payload
// with IEnumerable<object> to decide which.

static int RowsFor(object payload)
{
    if (payload is IEnumerable<object> items) // 💥 List<int> is NOT IEnumerable<object>
        return items.Count();
    return 1;                                  // treat it as a single scalar value
}

var tags   = new List<string> { "alpha", "beta", "gamma" }; // 3 text tags
var scores = new List<int>    { 10, 20, 30 };               // 3 numeric scores

int tagRows   = RowsFor(tags);
int scoreRows = RowsFor(scores);

Console.WriteLine($"tags (List<string>): {tagRows} rows; scores (List<int>): {scoreRows} rows.");

if (scoreRows != 3)
{
    throw new InvalidOperationException(
        $"exported {scoreRows} row for a 3-item List<int> - covariance is reference-types-only, so List<int> is not IEnumerable<object> and fell to the scalar path");
}

Console.WriteLine("Both collections exported every item.");
