// Exhibit #0077 - the fix: abort with normal control flow, not Environment.Exit.
//
// A `return` (or a throw) unwinds the stack the ordinary way, so the finally
// runs and the audit log is flushed. The caller-side code is identical to Bad.cs.

bool auditFlushed = false;

// The same auditor. ProcessExit fires even on Environment.Exit - unlike finally.
AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (auditFlushed) return;
    Console.WriteLine();
    Console.WriteLine(
        "AUDIT FAILURE: the batch exited with code 0 (success), but the finally that flushes the " +
        "audit log never ran - Environment.Exit terminates the process on the spot and skips every " +
        "pending finally, so the aborted run left no audit trail at all");
    Environment.ExitCode = 70;
};

RunBatch();
Console.WriteLine("Audit log flushed on every exit path. As it should be.");

void RunBatch()
{
    try
    {
        Console.WriteLine("[batch] charged invoice INV-1001 for $149.99");
        Console.WriteLine("[batch] poison record INV-1002 - stopping the batch");
        return; // unwinds normally, so the finally below still runs
    }
    finally
    {
        auditFlushed = true;
        Console.WriteLine("[batch] finally: audit log flushed");
    }
}
