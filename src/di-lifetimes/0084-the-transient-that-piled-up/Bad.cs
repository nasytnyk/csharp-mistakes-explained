// Exhibit #0084: transient IDisposables resolved from the root provider pile up.
//
// A worker resolves a fresh DbSession per job. "Transient" feels like "created,
// used, thrown away" - but the container tracks every IDisposable it creates and
// releases them only when the provider is disposed. Resolved from the root, that
// is app shutdown, so the sessions accumulate for the whole run.

#:package Microsoft.Extensions.DependencyInjection@9.*

using Microsoft.Extensions.DependencyInjection;

var provider = new ServiceCollection()
    .AddTransient<DbSession>()
    .BuildServiceProvider();

// Process a batch of jobs, resolving a session from the root provider each time.
for (int job = 0; job < 1000; job++)
{
    var session = provider.GetRequiredService<DbSession>(); // 💥 root-tracked; never released until shutdown
    session.Run($"job-{job}");
}

Console.WriteLine($"Sessions opened: {DbSession.Opened}, closed: {DbSession.Closed}");

// Self-audit: a per-job transient resource should be released after each job.
if (DbSession.Closed == 0)
    throw new InvalidOperationException(
        $"opened {DbSession.Opened} sessions and closed {DbSession.Closed}: a transient IDisposable resolved from " +
        "the root provider is held by the container for disposal until the provider itself is disposed - so every " +
        "session stays open for the whole run, a leak that grows one handle per job");

Console.WriteLine("Every session released.");

class DbSession : IDisposable
{
    public static int Opened, Closed;
    public DbSession() => Opened++;
    public void Run(string job) { /* ... use the connection ... */ }
    public void Dispose() => Closed++;
}
