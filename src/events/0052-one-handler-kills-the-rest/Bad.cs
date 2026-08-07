// Exhibit #0052: one throwing handler kills the rest

// An order-placed event fans out to three subscribers - email, analytics, audit.
// They run synchronously, in subscription order, on the publisher's one thread.

var ran = new List<string>();

var service = new OrderService();
service.OrderPlaced += o => ran.Add("email");                                          // #1 - runs
service.OrderPlaced += o => throw new InvalidOperationException("analytics API 503");  // #2 - flaky third party
service.OrderPlaced += o => ran.Add("audit");                                          // #3 - compliance record

service.Place("order-1001");

Console.WriteLine($"Handlers that ran: {string.Join(", ", ran)}");

// Self-audit: every order MUST be written to the audit log.
if (!ran.Contains("audit"))
{
    throw new InvalidOperationException(
        "the audit handler never ran - an earlier subscriber threw, which aborted the invocation list, " +
        "so every handler registered after it was silently skipped");
}

Console.WriteLine("Audit record written for every order.");

class OrderService
{
    public event Action<string>? OrderPlaced;

    public void Place(string order)
    {
        // Persist the order... then notify subscribers. A subscriber's failure should not crash
        // order placement, so we log it and carry on.
        try
        {
            OrderPlaced?.Invoke(order); // 💥 one Invoke walks the whole list; the first throw ends the walk
        }
        catch (Exception ex)
        {
            Console.WriteLine($"A subscriber failed while handling {order}: {ex.Message}");
        }
    }
}
