// Exhibit #0049: a record's ToString leaks its secrets

// A login attempt is logged. The Credentials record's generated ToString prints
// every member - including the ones that must never reach a log.

var creds = new Credentials("alice", "hunter2-super-secret", "tok_live_abc123");

// Structured or plain logging that passes the whole object - a universal pattern.
string logLine = $"Login attempt: {creds}"; // 💥 calls the generated ToString, which prints every member

Console.WriteLine(logLine);

if (logLine.Contains(creds.Password))
{
    throw new InvalidOperationException(
        "the log line contains the plaintext password - the record's generated ToString printed every member, secrets included");
}

Console.WriteLine("No secret reached the log.");

record Credentials(string User, string Password, string ApiToken);
