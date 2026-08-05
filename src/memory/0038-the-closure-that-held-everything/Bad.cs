// Exhibit #0038: one closure that held the whole scope

using System.Runtime.CompilerServices;

// An upload service. Each upload is processed once (a big buffer), then a small
// completion callback is kept to notify the user later. The buffer should be
// freed after processing - but a neighbouring lambda quietly pins it.

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
        "the upload buffer survived GC - the kept completion callback shares one closure with the validation lambda, so it roots 8 MB it never uses");
}

Console.WriteLine("Upload buffer was collected.");

// Handle one upload: process the buffer, keep a small completion callback.
[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference AcceptUpload(List<Action> pending, int uploadId)
{
    byte[] fileBytes = new byte[8 * 1024 * 1024];   // the upload payload

    // Validate the payload. A throwaway lambda that touches the bytes - it runs
    // once here and is never stored. But capturing fileBytes forces it into this
    // scope's single shared closure object.
    Action validate = () => Verify(fileBytes);
    validate();

    // The completion callback we keep. It needs only the id, never the bytes -
    // yet it lands in the SAME closure object as validate, so keeping it roots
    // fileBytes too.
    pending.Add(() => Console.Write($"upload {uploadId} done. ")); // 💥 kept; drags fileBytes along

    return new WeakReference(fileBytes);

    static void Verify(byte[] b) { if (b.Length == 0) throw new InvalidOperationException(); }
}
