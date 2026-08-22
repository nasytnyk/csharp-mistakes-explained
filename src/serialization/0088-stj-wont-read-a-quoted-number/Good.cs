// Exhibit #0088 - the fix: allow reading numbers from strings.
//
// JsonNumberHandling.AllowReadingFromString tells STJ to parse a quoted value
// into a numeric property. The payload and DTO are identical to Bad.cs.

#:property PublishAot=false

using System.Text.Json;
using System.Text.Json.Serialization;

string json = """{ "Quantity": "3", "UnitPrice": "9.99" }""";
Console.WriteLine($"Incoming payload: {json}");

var options = new JsonSerializerOptions
{
    NumberHandling = JsonNumberHandling.AllowReadingFromString, // accept "3" for an int
};

var order = JsonSerializer.Deserialize<Order>(json, options);

Console.WriteLine($"Parsed order: {order!.Quantity} x {order.UnitPrice:C}");

if (order.Quantity != 3 || order.UnitPrice != 9.99m)
    throw new InvalidOperationException(
        $"parsed {order.Quantity} x {order.UnitPrice} - expected 3 x 9.99");

Console.WriteLine("Quoted numbers parsed. As it should be.");

record Order(int Quantity, decimal UnitPrice);
