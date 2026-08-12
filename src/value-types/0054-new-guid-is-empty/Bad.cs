// Exhibit #0054: new Guid() is empty, not new

// Each order is given a "fresh" id... or so it looks. `new Guid()` is the default
// Guid - all zeros - not a generated value, so every order shares the same id.

var orders = new List<Order>
{
    new Order(new Guid(), "Ada"),   // 💥 new Guid() == Guid.Empty
    new Order(new Guid(), "Grace"),
    new Order(new Guid(), "Linus"),
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
        $"{orders.Count} orders collapsed to {byId.Count} entry - `new Guid()` is Guid.Empty (all zeros), " +
        "not a fresh id, so every order got the same id and each overwrote the last");
}

Console.WriteLine("Every order kept its own id.");

record Order(Guid Id, string Customer);
