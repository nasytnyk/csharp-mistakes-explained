// Exhibit #0036: the fix

// "Charge the card, but do not wait forever: if it takes too long, time out and
// retry." Same idiom - but the timeout now cancels the work instead of walking
// away from it, so the abandoned attempt cannot charge anything.

int charges = 0;                              // times the card actually moved money
var slowNetwork = new TaskCompletionSource(); // gates the first, "stuck" attempt

// The payment round-trip now takes a token and checks it before moving money.
async Task ChargeCardAsync(Task gateway, CancellationToken ct)
{
    await gateway.WaitAsync(ct);              // the network wait, now cancellable
    ct.ThrowIfCancellationRequested();        // never charge if we were told to stop
    Interlocked.Increment(ref charges);
}

// Attempt 1 hits a slow gateway and does not answer in time.
using var cts = new CancellationTokenSource();
var attempt1 = ChargeCardAsync(slowNetwork.Task, cts.Token);

// The "timeout". A Task.Delay that already elapsed stands in for the wait running
// out - it makes the timeout branch win deterministically, exactly as a real
// Task.Delay(5s) would once those five seconds pass.
var timeout = Task.CompletedTask;

if (await Task.WhenAny(attempt1, timeout) == timeout)
{
    cts.Cancel();                             // stop the abandoned work, not just the waiting
    Console.WriteLine($"Attempt 1 timed out (charges so far: {charges}). Retrying...");
}

// Attempt 2, the retry, reaches a healthy gateway and charges the card.
await ChargeCardAsync(Task.CompletedTask, CancellationToken.None);
Console.WriteLine($"Retry succeeded. Order #1001 is paid. Charges: {charges}.");

// A second later, the first attempt's reply arrives - but it was cancelled, so it
// throws before the charge line instead of moving money. We observe that.
slowNetwork.SetResult();
try { await attempt1; }
catch (OperationCanceledException) { /* expected: the timeout stopped attempt1 */ }

Console.WriteLine($"Final charge count for order #1001: {charges}.");

if (charges != 1)
{
    throw new InvalidOperationException(
        $"order #1001 charged {charges} times - the timed-out attempt was never stopped, only ignored");
}

Console.WriteLine("Order #1001 charged exactly once. As it should be.");
