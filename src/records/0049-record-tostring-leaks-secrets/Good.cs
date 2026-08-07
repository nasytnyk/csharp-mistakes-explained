// Exhibit #0049: the fix

using System.Text;

// The same login logging - but the record redacts its secrets from ToString via
// the PrintMembers hook, so logging the whole object no longer leaks them.

var creds = new Credentials("alice", "hunter2-super-secret", "tok_live_abc123");

string logLine = $"Login attempt: {creds}";

Console.WriteLine(logLine);

if (logLine.Contains(creds.Password))
{
    throw new InvalidOperationException("the log line contains the plaintext password");
}

Console.WriteLine("No secret reached the log. As it should be.");

record Credentials(string User, string Password, string ApiToken)
{
    // The record's ToString extensibility hook: emit only what is safe to log.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"User = {User}, Password = ***, ApiToken = ***");
        return true;
    }
}
