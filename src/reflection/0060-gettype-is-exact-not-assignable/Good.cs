// Exhibit #0060: the fix

// A dispatcher routes events to handlers registered by type. We registered one handler for
// PaymentEvent, expecting it to cover PaymentEvent and every subclass of it.
var handlers = new Dictionary<Type, Action<PaymentEvent>>
{
    [typeof(PaymentEvent)] = e => Console.WriteLine($"  handled {e.GetType().Name} (amount {e.Amount})"),
};

PaymentEvent[] incoming =
{
    new PaymentEvent { Amount = 100m },
    new RefundEvent  { Amount = 30m },   // a subclass of PaymentEvent
};

int handled = 0;
foreach (var e in incoming)
{
    Console.WriteLine($"{e.GetType().Name}: (e is PaymentEvent) = {e is PaymentEvent}");
    // match a handler whose registered type is assignable FROM the event's runtime type
    var handler = handlers.FirstOrDefault(reg => reg.Key.IsAssignableFrom(e.GetType())).Value;
    if (handler is not null)
    {
        handler(e);
        handled++;
    }
    else
    {
        Console.WriteLine($"  no handler for {e.GetType().Name} - dropped");
    }
}

Console.WriteLine($"Handled {handled} of {incoming.Length} events");

// Self-audit: every event IS a PaymentEvent, so the one registered handler must cover them all.
if (handled != incoming.Length)
{
    throw new InvalidOperationException(
        $"handled {handled} of {incoming.Length}");
}

Console.WriteLine("Every event reached a handler. As it should be.");

class PaymentEvent { public decimal Amount { get; init; } }
class RefundEvent : PaymentEvent { }
