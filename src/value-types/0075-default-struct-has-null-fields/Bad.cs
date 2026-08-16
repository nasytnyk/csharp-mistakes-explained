// Exhibit #0075: a default struct skips its constructor - reference fields are null

// A cart struct that "always" initializes its item list in its constructor.
var carts = new Cart[1]; // 💥 array elements are default(Cart): no constructor ran, Items is null

Console.WriteLine($"Cart from new():  Items null? {new Cart().Items is null}");
Console.WriteLine($"Cart from array:  Items null? {carts[0].Items is null}");

carts[0].Items.Add("SKU-1"); // NullReferenceException - Items was never constructed

Console.WriteLine($"Cart has {carts[0].Items.Count} item(s)");

// Self-audit (unreached: .Add throws on the null Items above).
if (carts[0].Items.Count != 1)
{
    throw new InvalidOperationException("the item was not added");
}

Console.WriteLine("Item added.");

struct Cart
{
    public List<string> Items;
    public Cart() => Items = new();
}
