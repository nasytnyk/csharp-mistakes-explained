// Exhibit #0060: the fix

// Audit the payment events in a batch. A refund IS a kind of payment, so it must count too.
PaymentEvent[] batch =
{
    new PaymentEvent { Amount = 100m },
    new RefundEvent  { Amount = 30m },   // a subclass of PaymentEvent
};

int payments = 0;
foreach (var e in batch)
{
    bool matched = typeof(PaymentEvent).IsAssignableFrom(e.GetType()); // base type accepts any subclass
    Console.WriteLine($"{e.GetType().Name}: matched={matched}  (e is PaymentEvent = {e is PaymentEvent})");
    if (matched)
        payments++;
}

Console.WriteLine($"Counted {payments} payment events of {batch.Length}");

// Self-audit: RefundEvent : PaymentEvent, so every event in the batch is a payment event.
if (payments != batch.Length)
{
    throw new InvalidOperationException(
        $"counted {payments} of {batch.Length}");
}

Console.WriteLine("Every payment event counted. As it should be.");

class PaymentEvent { public decimal Amount { get; init; } }
class RefundEvent : PaymentEvent { }
