// Exhibit #0086: an async lambda passed as an Action becomes async void.
//
// The batch "saves every order, then reports how many." List.ForEach takes an
// Action, so the async lambda is compiled as async void: ForEach kicks off all
// three saves and returns immediately, before any of them has finished.

var orders = new[] { "INV-1001", "INV-1002", "INV-1003" };
var saved = new List<string>();

orders.ToList().ForEach(async id => await SaveAsync(id)); // 💥 async lambda -> async void, never awaited

Console.WriteLine($"Saved {saved.Count} of {orders.Length} orders.");

// Self-audit: every order must be saved before we report success.
if (saved.Count != orders.Length)
    throw new InvalidOperationException(
        $"reported success but saved {saved.Count} of {orders.Length}: List.ForEach takes an Action, so the async " +
        "lambda became async void - ForEach started every save and returned without awaiting any, and the program " +
        "moves on (and exits) while the fire-and-forget saves are still pending and any failure goes unobserved");

Console.WriteLine("All orders saved.");

async Task SaveAsync(string id)
{
    await Task.Delay(20); // ... persist to the database ...
    saved.Add(id);
}
