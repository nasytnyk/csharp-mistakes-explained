# 🗑️ disposal

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-unflushed-tail (A5)

- **Twist:** Forget to dispose a StreamWriter and the file ends mid-sentence:
  the last buffer never flushes, the process exits with code 0, and the
  export is quietly short.
- **Mechanic:** StreamWriter buffers characters; Dispose/Flush writes the
  tail. Nothing else does: there is no flushing finalizer, and normal process
  exit does not flush live writers. The file contains a whole number of
  buffer-fulls - in the verification run, exactly 4096 of 8400 bytes. A
  small file whose content fits one buffer is 0 bytes: created, empty,
  "successfully written".
- **Who hits it:** quick export/report scripts and log writers - `new
  StreamWriter(path)` without `using`, in code where "it worked when I
  tested it" (the test data fit the buffer... or didn't, and the file being
  short went unnoticed).
- **Repro:** write ~200 lines, don't dispose, read the file back (StreamWriter
  opens with FileShare.Read, so a same-process FileStream with
  FileShare.ReadWrite can read it): length is less than written. Also show
  the 10-line case: 0 bytes. Keep the writer alive with GC.KeepAlive so the
  finalizer story doesn't muddy the demo. Deterministic, no packages.
- **Damage:** the nightly export is missing its last N rows; the file exists,
  opens, and parses, so reconciliation finds the loss weeks later - money and
  audit stakes, screenshots well.
- **😈 seed:** how *much* is lost depends on buffer alignment, so every run
  loses a different tail - the bug report is never reproducible.
- **Verified:** ran on .NET 10 (2026-07-22): 4096 &lt; 8400 bytes; small file
  0 bytes.

### the-cleanup-that-never-came (A5,6)

- **Twist:** "The GC will clean it up" - the GC provably collects the object
  and provably never calls Dispose: collected and leaked at the same time.
- **Mechanic:** the GC runs *finalizers*, and Dispose is not a finalizer -
  they are unrelated methods that most classes never connect. A type with
  Dispose and no finalizer (most application classes) simply never runs its
  cleanup when abandoned. A WeakReference proves collection happened; a flag
  in Dispose proves cleanup did not.
- **Who hits it:** everyone who has answered "what if I forget Dispose?"
  with "the GC handles it eventually": connections not returned to pools,
  transactions never completed, file locks held.
- **Repro:** class whose Dispose sets a static flag, no finalizer; create it
  in a `[MethodImpl(MethodImplOptions.NoInlining)]` helper, keep only a
  WeakReference; `GC.Collect()` + `WaitForPendingFinalizers()` + `Collect()`;
  assert the reference is dead AND the flag is false. Forced GC keeps it
  deterministic. No packages.
- **Damage:** pooled resources drain until the "restart fixes it" incident;
  the leak survives every memory profiler check, because the *memory* is
  fine.
- **BORDERLINE flag:** closest of the current batch to the primer floor -
  "GC doesn't call Dispose" is teachable-quote material. Kept because the
  collected-yet-not-cleaned double proof is a genuine "wait, both?"; the
  curator judges.
- **Verified:** ran on .NET 10 (2026-07-22): WeakReference dead, Dispose flag
  false.

### the-callback-that-outlived-dispose (A1,5)

- **Twist:** `timer.Dispose()` returns while the callback is still running -
  Dispose is a request, not a barrier, so "stop the timer, then tear down
  its state" tears the floor out from under a live callback.
- **Mechanic:** System.Threading.Timer.Dispose() only prevents *future*
  callbacks; one already in flight keeps running after Dispose returns.
  The synchronizing forms are `DisposeAsync()` - its ValueTask completes
  only when callbacks have drained (verified) - and the documented
  `Dispose(WaitHandle)` overload.
- **Who hits it:** every shutdown/teardown sequence shaped "dispose the
  timer, then dispose the resources the callback uses" - heartbeat, flush,
  and cache-refresh timers in services; the failure surfaces as a rare
  ObjectDisposedException at deploy or shutdown moments.
- **Repro:** gate the callback on a TaskCompletionSource: await callback
  start, call Dispose, print that Dispose returned while the callback's
  completion TCS is still pending, then open the gate. Contrast
  DisposeAsync, whose ValueTask stays incomplete until the callback ends.
  Gates make it fully deterministic. No packages.
- **Damage:** shutdown-time use-after-dispose - the callback writes to an
  already-disposed connection or file. In production the window is
  milliseconds, so it lands as "random crash during redeploy": the least
  reproducible, most shrugged-at class of bug.
- **😈 seed:** tests never see it - test callbacks finish in microseconds,
  production callbacks that flush buffers or call APIs stretch the window
  to seconds. The bug's severity is proportional to how much real work the
  callback does.
- **Verified:** ran on .NET 10 (2026-07-22): Dispose returned with the
  callback provably mid-flight; DisposeAsync's ValueTask completed only
  after the callback finished.

### using-var-disposes-late (A6,1)

- **Twist:** `using var` has no closing brace - the resource lives to the
  end of the *method*, so the file you "closed" 200 lines ago is still
  exclusively locked while the rest of the method runs.
- **Mechanic:** a using declaration disposes at enclosing-scope exit, which
  for the typical top-level `using var` means method end. Converting
  `using (...) { }` blocks to `using var` during modernization silently
  extends every resource's lifetime to the method tail. The lock is real:
  File.Open with FileShare.None stays exclusive until then - .NET enforces
  sharing modes on Linux too (verified).
- **Who hits it:** IDE-suggested "simplify to using declaration"
  refactors, then the method grows below; DB connections held through slow
  post-processing, files still locked when the next component reaches for
  them.
- **Repro:** a method opens a file via `using var` with FileShare.None,
  writes, is logically done - then mid-method a re-open attempt fails with
  IOException "being used by another process"; the same open succeeds one
  line after the method returns. Deterministic, no packages, verified
  cross-platform behavior on Linux.
- **Damage:** lock contention and "file in use" failures introduced by a
  *style refactor* - the diff that caused it changed braces, not logic, so
  the investigation never looks there.
- **😈 seed:** the connection-pool version: a pooled DbConnection held to
  method end shrinks the effective pool by the length of your longest
  method - the app slows under load with zero errors and a clean profile.
- **Verified:** ran on .NET 10 (2026-07-22): mid-method re-open failed with
  IOException, post-method open succeeded (Linux).

### exit-skips-every-using (A5)

- **Twist:** Environment.Exit(0) walks straight past every `using` and
  `finally` in flight - the exporter reports success, exit code 0, and the
  file it "wrote" is 0 bytes.
- **Mechanic:** Environment.Exit terminates the process without unwinding
  the stack: no finally blocks, no using disposal, no writer flush. It is
  the-unflushed-tail's symptom with a *success code attached* - the loss
  wears a green checkmark.
- **Who hits it:** CLI tools and scripts that call Environment.Exit(code)
  deep in the call stack for error and short-circuit paths - and the
  "clean" success exit that happens to sit above a still-buffered writer.
- **Repro:** the proof lives after process death, so the file re-execs
  itself: `Process.Start(Environment.ProcessPath, "child")` - the child
  writes 50 lines inside a using and calls Environment.Exit(0); the parent
  waits and reads back 0 bytes of ~1650 written, exit code 0. The
  re-exec-self technique works under file-based dotnet run (verified).
  Deterministic, no packages.
- **Damage:** exports and logs truncated on runs that *report success* -
  the exit code says trust me, and reconciliation finds the hole weeks
  later. Money and audit stakes.
- **NOTE:** promoted from this hall's seed list; could alternatively fold
  into the-unflushed-tail as its 😈 ("even with using, Exit still loses
  the tail") - the curator's call.
- **😈 seed:** at the end of Main, `return` and `Environment.Exit(0)` look
  interchangeable - one flushes everything, the other loses the tail, and
  every test that only checks the exit code passes both.
- **Verified:** ran on .NET 10 (2026-07-22): child exit code 0, file
  present at 0 bytes of ~1650 written; Environment.ProcessPath re-exec
  confirmed under file-based dotnet run.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **double-dispose-crashes** (A5) - a `using` block plus one explicit
  `Dispose()` calls Dispose twice; a non-idempotent implementation throws
  ObjectDisposedException, turning tidy cleanup into a crash.
