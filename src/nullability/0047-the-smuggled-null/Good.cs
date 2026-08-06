// Exhibit #0047: the fix
#:property PublishAot=false

using System.Text.Json;

// The same order boundary - but deserialization is told to respect nullable
// annotations, so an explicit null for a non-nullable property is rejected right
// here, at the edge, instead of smuggled downstream.

var payload = """{ "CustomerId": null, "Total": 42.00 }""";

var options = new JsonSerializerOptions { RespectNullableAnnotations = true };

try
{
    var order = JsonSerializer.Deserialize<Order>(payload, options)!;
    Console.WriteLine($"Processing order for customer {order.CustomerId.ToUpperInvariant()}.");
}
catch (JsonException ex)
{
    Console.WriteLine($"Rejected the order at the boundary: {ex.Message}");
}

Console.WriteLine("The null never got past the edge. As it should be.");

sealed class Order
{
    public string CustomerId { get; set; } = ""; // non-nullable, even initialized
    public decimal Total { get; set; }
}
