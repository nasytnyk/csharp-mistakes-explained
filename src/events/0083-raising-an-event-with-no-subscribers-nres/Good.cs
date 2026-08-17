// Exhibit #0083 - the fix: raise the event with the null-conditional invoke.
//
// `OrderPlaced?.Invoke(...)` calls handlers when there are any and is a no-op
// when the delegate is null. The rest is identical to Bad.cs.

var service = new OrderService();

// (The reporting module that normally subscribes is not wired up on this path.)

Console.WriteLine("Placing order INV-1001...");
service.PlaceOrder("INV-1001", 149.99m);
Console.WriteLine("Order placed. As it should be.");

class OrderService
{
    public event EventHandler<string>? OrderPlaced;

    public void PlaceOrder(string id, decimal amount)
    {
        // ... persist the order ...
        OrderPlaced?.Invoke(this, id); // no subscribers -> no-op, no crash
    }
}
