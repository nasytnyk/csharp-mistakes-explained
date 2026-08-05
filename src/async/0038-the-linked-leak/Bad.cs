// Exhibit #0038: a linked CancellationTokenSource never disposed

using System.Runtime.CompilerServices;

// A long-running service. The host's shutdown token lives for the whole process;
// each request derives a linked token from it for its own timeout/cancellation.
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
        $"{leaked} of 3 request states survived GC - each undisposed linked CTS stays hooked to the app-shutdown token, which will not release it until the process exits");
}

Console.WriteLine("Every request's state was collected.");

// One request: derive a linked token from the app token, register per-request
// cleanup on it, serve the request. Then return - dropping the linked CTS.
[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference HandleRequest(CancellationToken appToken, int id)
{
    var state = new RequestState(id);
    var linked = CancellationTokenSource.CreateLinkedTokenSource(appToken); // 💥 never disposed
    linked.Token.Register(() => state.Cleanup());                           // cleanup callback captures the request state
    // ... serve the request using linked.Token ...
    return new WeakReference(state);
}

sealed class RequestState(int id)
{
    private readonly byte[] _payload = new byte[64 * 1024]; // the request's working memory
    public int Id { get; } = id;
    public void Cleanup() => Array.Clear(_payload);         // release buffers on cancellation
}
