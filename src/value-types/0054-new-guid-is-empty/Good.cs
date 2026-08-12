// Exhibit #0054: the fix

// The same orders - but each id is generated with Guid.NewGuid(), the factory that
// actually produces a fresh value, so every order gets its own id.

var orders = new List<Order>
{
    new Order(Guid.NewGuid(), "Ada"),   // Guid.NewGuid() generates a fresh v4 GUID
    new Order(Guid.NewGuid(), "Grace"),
    new Order(Guid.NewGuid(), "Linus"),
};

// Index them by id, the way a repository or cache would.
var byId = new Dictionary<Guid, Order>();
foreach (var order in orders)
    byId[order.Id] = order;

Console.WriteLine($"Created {orders.Count} orders; distinct ids stored: {byId.Count}");
foreach (var order in orders)
    Console.WriteLine($"  {order.Customer}: {order.Id}");

if (byId.Count != orders.Count)
{
    throw new InvalidOperationException(
        $"{orders.Count} orders collapsed to {byId.Count} entry");
}

Console.WriteLine("Every order kept its own id. As it should be.");

record Order(Guid Id, string Customer);
