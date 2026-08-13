// Exhibit #0060: GetType() is exact, not assignable

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
    if (handlers.TryGetValue(e.GetType(), out var handler)) // 💥 GetType() is the EXACT runtime type
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
        $"handled {handled} of {incoming.Length} - a table keyed by GetType() matches the EXACT runtime type, " +
        "so RefundEvent (a PaymentEvent subclass) never finds the PaymentEvent handler and is silently dropped");
}

Console.WriteLine("Every event reached a handler.");

class PaymentEvent { public decimal Amount { get; init; } }
class RefundEvent : PaymentEvent { }
