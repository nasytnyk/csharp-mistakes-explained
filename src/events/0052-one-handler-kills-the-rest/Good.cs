// Exhibit #0052: the fix

// The same order-placed fan-out - but the publisher raises each subscriber itself,
// isolating every handler, so one that throws cannot stop the others.

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
        "the audit handler never ran");
}

Console.WriteLine("Audit record written for every order. As it should be.");

class OrderService
{
    public event Action<string>? OrderPlaced;

    public void Place(string order)
    {
        // Persist the order... then notify subscribers - each one isolated, so a single
        // failure is logged and the rest of the list still runs.
        var handlers = OrderPlaced;
        if (handlers is null) return;

        foreach (Action<string> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(order);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"A subscriber failed while handling {order}: {ex.Message}");
            }
        }
    }
}
