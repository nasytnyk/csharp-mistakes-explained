// Exhibit #0048: the fix
#:package xunit.assert@2.9.3
#:property PublishAot=false

using Xunit;

// The same report test - but it asserts membership, not order, with
// Assert.Equivalent, which is order-insensitive for collections.

string[] expected = ["billing", "shipping", "support"];
string[] actual = ActiveTags(); // the same three tags, a different order

Console.WriteLine($"Expected: [{string.Join(", ", expected)}]");
Console.WriteLine($"Actual:   [{string.Join(", ", actual)}]");

Assert.Equivalent(expected, actual); // order-insensitive: same members pass

Console.WriteLine("Report tags match (any order). As it should be.");

// Stands in for a GroupBy / Dictionary-keys / SQL-without-ORDER-BY result:
// correct membership, order not guaranteed.
static string[] ActiveTags() => ["support", "billing", "shipping"];
