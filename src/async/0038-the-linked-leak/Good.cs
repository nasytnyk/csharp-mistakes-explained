// Exhibit #0038: the fix

using System.Runtime.CompilerServices;

// The same service - but each request's linked CTS is wrapped in `using`, so it
// unhooks from the app-shutdown token the moment the request ends.
var appStopping = new CancellationTokenSource();

// Handle three requests. We keep only a WeakReference to each request's state -
// our code holds no strong reference, so after a full GC it should all be gone.
var traces = new WeakReference[3];
for (int i = 0; i < traces.Length; i++)
    traces[i] = HandleRequest(appStopping.Token, i + 1);

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

int leaked = traces.Count(t => t.IsAlive);
Console.WriteLine($"Three requests handled; their state should be collected. Still in memory after GC: {leaked} of 3.");

GC.KeepAlive(appStopping); // the app-shutdown token outlives every request, as in production

if (leaked != 0)
{
    throw new InvalidOperationException(
        $"{leaked} of 3 request states survived GC despite disposal");
}

Console.WriteLine("Every request's state was collected. As it should be.");

// One request: derive a linked token from the app token, register per-request
// cleanup on it, serve the request. `using` disposes the linked CTS on the way out.
[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference HandleRequest(CancellationToken appToken, int id)
{
    var state = new RequestState(id);
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(appToken); // dispose unhooks it from appToken
    linked.Token.Register(() => state.Cleanup());                                 // cleanup callback captures the request state
    // ... serve the request using linked.Token ...
    return new WeakReference(state);
}

sealed class RequestState(int id)
{
    private readonly byte[] _payload = new byte[64 * 1024]; // the request's working memory
    public int Id { get; } = id;
    public void Cleanup() => Array.Clear(_payload);         // release buffers on cancellation
}
