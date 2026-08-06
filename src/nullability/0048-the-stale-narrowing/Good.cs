// Exhibit #0048: the fix

// The same handler - but it snapshots the field into a local before the work, so
// the value it validated is the value it uses. A method call cannot change a local.

new Session().Handle();

sealed class Session
{
    private string? _currentUser = "Ada";

    public void Handle()
    {
        var user = _currentUser;  // snapshot: this local is what we narrow and use
        if (user != null)
        {
            Console.WriteLine($"User {user} passed the guard.");
            FinishAudit();        // clears the field, but `user` still holds the validated value
            Console.WriteLine($"Finishing request for {user.ToUpperInvariant()}. As it should be.");
        }
    }

    // End-of-request cleanup: write the audit record and clear the session.
    private void FinishAudit() => _currentUser = null;
}
