// Exhibit #0086 - the fix: await each save in a real loop.
//
// A foreach with await runs the Task-returning method and waits for it, so all
// saves complete (and their failures propagate) before we report. The rest is
// identical to Bad.cs.

var orders = new[] { "INV-1001", "INV-1002", "INV-1003" };
var saved = new List<string>();

foreach (var id in orders)
    await SaveAsync(id); // await each save before moving on

Console.WriteLine($"Saved {saved.Count} of {orders.Length} orders.");

if (saved.Count != orders.Length)
    throw new InvalidOperationException(
        $"reported success but saved {saved.Count} of {orders.Length}");

Console.WriteLine("All orders saved. As it should be.");

async Task SaveAsync(string id)
{
    await Task.Delay(20); // ... persist to the database ...
    saved.Add(id);
}
