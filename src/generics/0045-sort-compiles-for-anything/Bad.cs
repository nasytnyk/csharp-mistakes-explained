// Exhibit #0045: Sort compiles for anything

// A monthly report sorts its rows into chronological order by a Period key.
// Period is a record - and OrderBy carries no IComparable constraint, so ordering
// by it compiles and ships.

// The unit test has one row. One row needs no comparisons, so it "sorts" fine.
var testRows = new List<Row> { new(new Period(2026, 1), 500m) };
_ = testRows.OrderBy(r => r.Period).ToList();
Console.WriteLine($"Test set of {testRows.Count} row sorted without complaint.");

// Production has a real month's worth of rows.
var rows = new List<Row>
{
    new(new Period(2026, 3), 500m),
    new(new Period(2026, 1), 300m),
    new(new Period(2026, 2), 400m),
};

// 💥 the first comparison of two Period records has nothing to call: no CompareTo
var sorted = rows.OrderBy(r => r.Period).ToList();

Console.WriteLine($"Report sorted into {sorted.Count} rows.");

record Period(int Year, int Month);
record Row(Period Period, decimal Total);
