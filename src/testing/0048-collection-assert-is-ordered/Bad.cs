// Exhibit #0048: Assert.Equal compares collections in order
#:package xunit.assert@2.9.3
#:property PublishAot=false

using Xunit;

// A test for the "active support tags" report. Production groups tickets by tag,
// so the tags come back in whatever order the grouping produced - the order is
// incidental, the membership is the requirement.

string[] expected = ["billing", "shipping", "support"];
string[] actual = ActiveTags(); // the same three tags, a different order

Console.WriteLine($"Expected: [{string.Join(", ", expected)}]");
Console.WriteLine($"Actual:   [{string.Join(", ", actual)}]");

Assert.Equal(expected, actual); // 💥 same members, different order - Assert.Equal compares in order

Console.WriteLine("Report tags match.");

// Stands in for a GroupBy / Dictionary-keys / SQL-without-ORDER-BY result:
// correct membership, order not guaranteed.
static string[] ActiveTags() => ["support", "billing", "shipping"];
