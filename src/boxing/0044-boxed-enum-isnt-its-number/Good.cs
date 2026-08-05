// Exhibit #0044: the fix

// The same dispatch table - but keyed by the enum type, and the wire's number is
// converted into that enum at the boundary, so keys and lookups are one type.

var handlers = new Dictionary<Status, string>
{
    [Status.Approved] = "send-receipt", // key is a Status, not an object
};

object statusFromWire = 1; // the wire delivered "Approved" as the number 1

Status status = (Status)(int)statusFromWire; // parse the wire number into the domain enum at the edge

Console.WriteLine($"Parsed the wire's {statusFromWire} into {status}.");

if (!handlers.TryGetValue(status, out var handler))
{
    throw new InvalidOperationException($"no handler for status {status}");
}

Console.WriteLine($"Dispatching to {handler}. As it should be.");

enum Status { Pending, Approved, Rejected }
