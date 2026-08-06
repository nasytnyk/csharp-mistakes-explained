// Exhibit #0048: a stale narrowing survives a method call

// A session handler guards on the current user, does its work, then finishes up.
// The guard narrows _currentUser to non-null - and the compiler keeps trusting
// that narrowing across the FinishAudit() call, which quietly clears the session.

new Session().Handle();

sealed class Session
{
    private string? _currentUser = "Ada";

    public void Handle()
    {
        if (_currentUser != null) // narrows _currentUser to non-null for the rest of the block
        {
            Console.WriteLine($"User {_currentUser} passed the guard.");
            FinishAudit();        // clears the session - but flow analysis does not model the side effect
            Console.WriteLine($"Finishing request for {_currentUser.ToUpperInvariant()}."); // 💥 NRE, warning-free
        }
    }

    // End-of-request cleanup: write the audit record and clear the session.
    private void FinishAudit() => _currentUser = null;
}
