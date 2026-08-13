// Exhibit #0060: the fix

#:property PublishAot=false

using System.Reflection;

// A hand-rolled mapper: fill an object's properties from a row of string cells (a CSV line,
// a config section, query params), converting each cell to the property's declared type.
var row = new Dictionary<string, string>
{
    ["Qty"] = "5",
    ["Discount"] = "10", // Discount is optional (int?) - and this row actually carries a value
};

var line = new OrderLine();
foreach (var prop in typeof(OrderLine).GetProperties())
{
    if (!row.TryGetValue(prop.Name, out var cell))
        continue;

    Console.WriteLine($"mapping {prop.Name} ({prop.PropertyType.Name}) <- \"{cell}\"");
    var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType; // unwrap int? -> int
    var value = Convert.ChangeType(cell, target);   // convert to the underlying type
    prop.SetValue(line, value);                     // a boxed int assigns straight into an int? property
    Console.WriteLine($"  -> {value}");
}

Console.WriteLine($"Mapped Qty={line.Qty}, Discount={line.Discount}");

// Self-audit: the optional column carried a value, so it must map, not crash.
if (line.Discount != 10)
{
    throw new InvalidOperationException($"Discount mapped to {line.Discount}, expected 10");
}

Console.WriteLine("Row mapped. As it should be.");

class OrderLine
{
    public int Qty { get; set; }
    public int? Discount { get; set; }
}
