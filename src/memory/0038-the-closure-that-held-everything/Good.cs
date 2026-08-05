// Exhibit #0038: the fix

using System.Runtime.CompilerServices;

// The same upload service - but the big buffer and its validation lambda live in
// their own block, so they get their own closure object and the kept completion
// callback cannot pin the buffer.

var pending = new List<Action>();                 // long-lived: callbacks for open uploads
var uploadBuffer = AcceptUpload(pending, uploadId: 7);

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

Console.WriteLine($"Upload processed; its 8 MB buffer should be freed. Still alive after GC: {uploadBuffer.IsAlive}.");

GC.KeepAlive(pending); // the pending callbacks outlive the request, as on a real server

if (uploadBuffer.IsAlive)
{
    throw new InvalidOperationException(
        "the upload buffer survived GC despite the split scope");
}

Console.WriteLine("Upload buffer was collected. As it should be.");

// Handle one upload: process the buffer, keep a small completion callback.
[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference AcceptUpload(List<Action> pending, int uploadId)
{
    WeakReference bufferRef;
    {
        byte[] fileBytes = new byte[8 * 1024 * 1024]; // the upload payload

        // The validation lambda captures fileBytes into THIS block's closure.
        Action validate = () => Verify(fileBytes);
        validate();

        bufferRef = new WeakReference(fileBytes);
    } // this block's closure (holding fileBytes) is now unreachable

    // The completion callback captures only the id, in a separate scope - so it
    // gets its own closure object and never touches fileBytes.
    pending.Add(() => Console.Write($"upload {uploadId} done. "));

    return bufferRef;

    static void Verify(byte[] b) { if (b.Length == 0) throw new InvalidOperationException(); }
}
