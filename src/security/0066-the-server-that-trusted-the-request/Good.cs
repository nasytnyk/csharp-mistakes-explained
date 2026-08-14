// Exhibit #0066: the fix

using System.Globalization;

// Pin formatting so the demo reads the same on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// The catalog the server owns - the source of truth for prices.
var catalog = new Dictionary<string, decimal> { ["SKU-1"] = 49.99m };

// The order request that arrives from the client (a JSON body, model-bound).
var req = new OrderRequest(ProductId: "SKU-1", Quantity: 1, UnitPrice: 0.01m, Status: "Paid");

// Build the order, but take money and status from the server, not the request.
var order = new Order
{
    ProductId = req.ProductId,
    Quantity = req.Quantity,
    UnitPrice = catalog[req.ProductId], // price from the catalog, keyed by ProductId
    Status = "Pending",                 // the server owns the order lifecycle
};
decimal charged = order.Quantity * order.UnitPrice;

Console.WriteLine($"Charged {charged:0.00} for {order.Quantity} x {order.ProductId}, status {order.Status}");

// Self-audit: the price must come from the catalog, and the status must be server-controlled.
decimal catalogTotal = catalog[req.ProductId] * req.Quantity;
if (charged != catalogTotal)
{
    throw new InvalidOperationException(
        $"charged {charged:0.00} but the catalog total is {catalogTotal:0.00}");
}
if (order.Status != "Pending")
{
    throw new InvalidOperationException($"order status is '{order.Status}'");
}

Console.WriteLine("Order priced and staged by the server. As it should be.");

record OrderRequest(string ProductId, int Quantity, decimal UnitPrice, string Status);

class Order
{
    public string ProductId { get; init; } = "";
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Status { get; set; } = "";
}
