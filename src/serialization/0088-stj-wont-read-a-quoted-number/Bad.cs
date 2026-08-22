// Exhibit #0088: System.Text.Json won't read a quoted number.
//
// A partner posts an order; its loosely-typed client serializes numbers as
// strings. The DTO types them as int/decimal, and STJ refuses to convert a
// quoted value to a number - so the whole payload fails to deserialize.

#:property PublishAot=false

using System.Text.Json;

// Numbers arrive as strings, which is valid JSON - the client just quoted them.
string json = """{ "Quantity": "3", "UnitPrice": "9.99" }""";
Console.WriteLine($"Incoming payload: {json}");

var order = JsonSerializer.Deserialize<Order>(json); // 💥 JsonException: "3" is a string, not a number

Console.WriteLine($"Parsed order: {order!.Quantity} x {order.UnitPrice:C}");

record Order(int Quantity, decimal UnitPrice);
