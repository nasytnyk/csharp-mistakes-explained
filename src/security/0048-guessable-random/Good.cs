// Exhibit #0048: the fix

using System.Security.Cryptography;

// The same password reset - but the token comes from the cryptographic RNG, which
// has no seed to guess. The attacker still knows the account's creation time; it
// buys them nothing.

int accountCreatedAt = 1_700_000_000; // still knowable - and now irrelevant

string serverToken = ResetToken();

// The attacker has no seed, no recoverable state, nothing to reproduce.
string attackerToken = ResetToken();

Console.WriteLine($"Server issued reset token: {serverToken}");
Console.WriteLine($"Attacker's best guess:     {attackerToken}");
Console.WriteLine($"(The attacker still knows the account was created at {accountCreatedAt} - it buys nothing now.)");

if (serverToken == attackerToken) // two cryptographic tokens colliding is effectively impossible
{
    throw new InvalidOperationException("two independent cryptographic tokens collided");
}

Console.WriteLine("The token was unpredictable. As it should be.");

static string ResetToken()
{
    var bytes = RandomNumberGenerator.GetBytes(16); // cryptographically secure, unseeded
    return Convert.ToHexString(bytes);
}
