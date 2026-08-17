// Exhibit #0083: raising an event with no subscribers throws NullReferenceException.
//
// An OrderService announces each placement through an event. In this deployment
// nothing has subscribed on this path - and an event with no handlers is a null
// delegate, so invoking it crashes the publisher on the first order.

var service = new OrderService();

// (The reporting module that normally subscribes is not wired up on this path.)

Console.WriteLine("Placing order INV-1001...");
service.PlaceOrder("INV-1001", 149.99m);
Console.WriteLine("Order placed.");

class OrderService
{
    public event EventHandler<string>? OrderPlaced;

    public void PlaceOrder(string id, decimal amount)
    {
        // ... persist the order ...
        OrderPlaced(this, id); // 💥 no subscribers -> OrderPlaced is null -> NullReferenceException
    }
}
