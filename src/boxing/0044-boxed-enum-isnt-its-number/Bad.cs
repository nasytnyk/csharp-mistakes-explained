// Exhibit #0044: a boxed enum is not its number

// A dispatch table: one handler per order status, keyed in an object-typed map.
// Handlers are registered with the enum; the wire format (JSON, a config value)
// delivers the status as a plain number. The same value - to a cast.

var handlers = new Dictionary<object, string>
{
    [Status.Approved] = "send-receipt", // key is the enum, boxed as Status
};

object statusFromWire = 1; // the wire delivered "Approved" as the number 1

Console.WriteLine($"(int)Status.Approved is {(int)(object)Status.Approved}, the wire sent {statusFromWire} - equal by cast: {(int)(object)Status.Approved == (int)statusFromWire}.");

if (!handlers.TryGetValue(statusFromWire, out var handler)) // 💥 boxed int 1 is never Equal to boxed Status.Approved
{
    throw new InvalidOperationException(
        $"no handler for status {statusFromWire} - the table is keyed by the boxed enum, and a boxed int is never Equal to a boxed enum, even though the (int) cast says they are the same value");
}

Console.WriteLine($"Dispatching to {handler}.");

enum Status { Pending, Approved, Rejected }
