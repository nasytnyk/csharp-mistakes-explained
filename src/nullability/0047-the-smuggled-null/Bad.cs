// Exhibit #0047: a JSON null smuggled into a non-nullable property
#:property PublishAot=false

using System.Text.Json;

// An order arrives as JSON. The DTO says CustomerId is a non-null string - it even
// carries an initializer. But the wire sent null (a JS client serialized an
// absent field), and System.Text.Json ignores the annotation by default.

var payload = """{ "CustomerId": null, "Total": 42.00 }""";

var order = JsonSerializer.Deserialize<Order>(payload)!;

// The type system swears CustomerId is a non-null string, so this line is
// warning-free. At runtime it is null.
Console.WriteLine($"Processing order for customer {order.CustomerId.ToUpperInvariant()}."); // 💥 NRE

Console.WriteLine("Order processed.");

sealed class Order
{
    public string CustomerId { get; set; } = ""; // non-nullable, even initialized
    public decimal Total { get; set; }
}
