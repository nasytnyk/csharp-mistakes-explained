// Exhibit #0041: the fix

// The same reporting code - but it converts the boxed number instead of unboxing
// it, so it works whatever numeric type the provider happened to box.

object countCell = QueryScalar("SELECT COUNT(*) FROM orders"); // boxed as long

Console.WriteLine($"COUNT came back as {countCell} (a {countCell.GetType().Name} in the box).");
Console.WriteLine($"An int obviously fits: (long)42 == 42 is {(long)42 == 42}.");

int count = Convert.ToInt32(countCell); // converts any boxed numeric, whatever the provider boxed

Console.WriteLine($"Processing {count} orders.");

if (count != 42)
{
    throw new InvalidOperationException($"expected 42 orders, got {count}");
}

Console.WriteLine("Every order accounted for. As it should be.");

// Stands in for the data provider: COUNT(*) comes back boxed as a long.
static object QueryScalar(string sql) => 42L;
