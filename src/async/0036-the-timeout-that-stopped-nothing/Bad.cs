// Exhibit #0036: a WhenAny timeout that never stopped the work

// "Charge the card, but do not wait forever: if it takes too long, time out and
// retry." The classic Task.WhenAny(work, Task.Delay(...)) timeout idiom, wrapped
// around a payment.

int charges = 0;                              // times the card actually moved money
var slowNetwork = new TaskCompletionSource(); // gates the first, "stuck" attempt

// The payment round-trip: wait for the gateway, then charge the card exactly once.
async Task ChargeCardAsync(Task gateway)
{
    await gateway;                            // the network wait we are timing
    Interlocked.Increment(ref charges);
}

// Attempt 1 hits a slow gateway and does not answer in time.
var attempt1 = ChargeCardAsync(slowNetwork.Task);

// The "timeout". A Task.Delay that already elapsed stands in for the wait running
// out - it makes the timeout branch win deterministically, exactly as a real
// Task.Delay(5s) would once those five seconds pass.
var timeout = Task.CompletedTask;

if (await Task.WhenAny(attempt1, timeout) == timeout) // 💥 timeout wins; attempt1 is abandoned, never cancelled
{
    Console.WriteLine($"Attempt 1 timed out (charges so far: {charges}). Retrying...");
}

// Attempt 2, the retry, reaches a healthy gateway and charges the card.
await ChargeCardAsync(Task.CompletedTask);
Console.WriteLine($"Retry succeeded. Order #1001 is paid. Charges: {charges}.");

// A second later, the "timed-out" first attempt finally gets its reply - and
// charges the very same card again. Nobody ever told it to stop.
slowNetwork.SetResult();
await attempt1;

Console.WriteLine($"Final charge count for order #1001: {charges}.");

if (charges != 1)
{
    throw new InvalidOperationException(
        $"order #1001 charged {charges} times - the timed-out attempt was never stopped, only ignored");
}

Console.WriteLine("Order #1001 charged exactly once.");
