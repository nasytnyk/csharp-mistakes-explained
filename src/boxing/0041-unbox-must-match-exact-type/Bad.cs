// Exhibit #0041: unboxing demands the exact type

// Reading a row count from a query result. The result set hands values back as
// object, and different databases box numbers as different types: this provider
// returns COUNT as a long (SQLite does; so does SQL Server's COUNT_BIG). The
// reporting code unboxes it straight to int.

object countCell = QueryScalar("SELECT COUNT(*) FROM orders"); // boxed as long

Console.WriteLine($"COUNT came back as {countCell} (a {countCell.GetType().Name} in the box).");
Console.WriteLine($"An int obviously fits: (long)42 == 42 is {(long)42 == 42}.");

int count = (int)countCell; // 💥 InvalidCastException - unboxing needs the EXACT type, no conversion

Console.WriteLine($"Processing {count} orders.");

// Stands in for the data provider: COUNT(*) comes back boxed as a long.
static object QueryScalar(string sql) => 42L;
