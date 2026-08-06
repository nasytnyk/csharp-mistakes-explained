// Exhibit #0045: the fix

// The same report - but it orders by a comparable projection of the key: a tuple
// of its fields, which the runtime knows how to compare structurally.

var testRows = new List<Row> { new(new Period(2026, 1), 500m) };
_ = testRows.OrderBy(r => (r.Period.Year, r.Period.Month)).ToList();
Console.WriteLine($"Test set of {testRows.Count} row sorted without complaint.");

var rows = new List<Row>
{
    new(new Period(2026, 3), 500m),
    new(new Period(2026, 1), 300m),
    new(new Period(2026, 2), 400m),
};

// Order by the tuple (Year, Month) - ValueTuple implements structural comparison.
var sorted = rows.OrderBy(r => (r.Period.Year, r.Period.Month)).ToList();

Console.WriteLine($"Report sorted into {sorted.Count} rows: {string.Join(", ", sorted.Select(r => $"{r.Period.Year}-{r.Period.Month:D2}"))}.");

if (sorted[0].Period.Month != 1)
{
    throw new InvalidOperationException($"expected January first, got month {sorted[0].Period.Month}");
}

Console.WriteLine("Rows are in chronological order. As it should be.");

record Period(int Year, int Month);
record Row(Period Period, decimal Total);
