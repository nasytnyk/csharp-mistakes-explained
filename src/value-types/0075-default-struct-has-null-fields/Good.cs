// Exhibit #0075: the fix

// A cart struct that "always" initializes its item list in its constructor.
var carts = new Cart[1];
for (int i = 0; i < carts.Length; i++)
    carts[i] = new Cart(); // run the constructor for each slot, so Items is a real list

Console.WriteLine($"Cart from new():  Items null? {new Cart().Items is null}");
Console.WriteLine($"Cart from array:  Items null? {carts[0].Items is null}");

carts[0].Items.Add("SKU-1"); // Items is initialized now

Console.WriteLine($"Cart has {carts[0].Items.Count} item(s)");

// Self-audit: one SKU was added, so the cart must hold one item.
if (carts[0].Items.Count != 1)
{
    throw new InvalidOperationException("the item was not added");
}

Console.WriteLine("Item added. As it should be.");

struct Cart
{
    public List<string> Items;
    public Cart() => Items = new();
}
