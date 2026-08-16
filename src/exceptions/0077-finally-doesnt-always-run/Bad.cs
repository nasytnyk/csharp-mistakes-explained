// Exhibit #0077: finally does not always run - Environment.Exit skips it.
//
// A nightly payment batch flushes its audit log in a finally, on the belief
// that "whatever happens, the audit trail is written." A ProcessExit hook
// (which DOES run on Environment.Exit) audits whether that belief held.

bool auditFlushed = false;

// The auditor. ProcessExit fires even on Environment.Exit - unlike finally.
AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (auditFlushed) return;
    Console.WriteLine();
    Console.WriteLine(
        "AUDIT FAILURE: the batch exited with code 0 (success), but the finally that flushes the " +
        "audit log never ran - Environment.Exit terminates the process on the spot and skips every " +
        "pending finally, so the aborted run left no audit trail at all");
    Environment.ExitCode = 70; // turn the "clean" exit into a visible failure
};

RunBatch();

void RunBatch()
{
    try
    {
        Console.WriteLine("[batch] charged invoice INV-1001 for $149.99");
        Console.WriteLine("[batch] poison record INV-1002 - failing fast");
        Environment.Exit(0); // 💥 exits now; the finally below is skipped, not run
    }
    finally
    {
        auditFlushed = true;
        Console.WriteLine("[batch] finally: audit log flushed"); // never reached
    }
}
