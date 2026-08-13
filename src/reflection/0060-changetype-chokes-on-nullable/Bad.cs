// Exhibit #0060: Convert.ChangeType chokes on Nullable<T>

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
    var value = Convert.ChangeType(cell, prop.PropertyType); // 💥 throws for int? - Nullable<T> is not IConvertible
    prop.SetValue(line, value);
    Console.WriteLine($"  -> {value}");
}

Console.WriteLine($"Mapped Qty={line.Qty}, Discount={line.Discount}");

// Self-audit (never reached: ChangeType throws on the Discount column above).
if (line.Discount != 10)
{
    throw new InvalidOperationException($"Discount mapped to {line.Discount}, expected 10");
}

Console.WriteLine("Row mapped.");

class OrderLine
{
    public int Qty { get; set; }
    public int? Discount { get; set; }
}
