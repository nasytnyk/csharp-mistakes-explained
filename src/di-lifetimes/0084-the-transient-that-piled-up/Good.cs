// Exhibit #0084 - the fix: a scope per unit of work.
//
// Resolve the session from a scope, not the root. Disposing the scope releases
// every IDisposable created within it, so each job's session is closed as soon
// as the job ends. The rest is identical to Bad.cs.

#:package Microsoft.Extensions.DependencyInjection@9.*

using Microsoft.Extensions.DependencyInjection;

var provider = new ServiceCollection()
    .AddTransient<DbSession>()
    .BuildServiceProvider();

for (int job = 0; job < 1000; job++)
{
    using var scope = provider.CreateScope(); // one scope per job
    var session = scope.ServiceProvider.GetRequiredService<DbSession>();
    session.Run($"job-{job}");
    // scope disposed here -> the session resolved within it is disposed too
}

Console.WriteLine($"Sessions opened: {DbSession.Opened}, closed: {DbSession.Closed}");

if (DbSession.Closed != DbSession.Opened)
    throw new InvalidOperationException(
        $"opened {DbSession.Opened} sessions but closed {DbSession.Closed}");

Console.WriteLine("Every session released. As it should be.");

class DbSession : IDisposable
{
    public static int Opened, Closed;
    public DbSession() => Opened++;
    public void Run(string job) { /* ... use the connection ... */ }
    public void Dispose() => Closed++;
}
