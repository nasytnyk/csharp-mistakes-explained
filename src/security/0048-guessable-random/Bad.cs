// Exhibit #0048: System.Random for security tokens

// Password reset. The server issues a "random" reset token for the account, but
// seeds System.Random with the account's creation time - a value an attacker can
// read off the profile or brute-force within a small window.

int accountCreatedAt = 1_700_000_000; // Unix seconds - guessable, not a secret

string serverToken = ResetToken(accountCreatedAt);

// The attacker re-derives the same seed and runs the same line of code...
string attackerToken = ResetToken(accountCreatedAt);

Console.WriteLine($"Server issued reset token: {serverToken}");
Console.WriteLine($"Attacker reproduced:       {attackerToken}");

if (serverToken == attackerToken) // 💥 identical - the "random" token was a pure function of a guessable seed
{
    throw new InvalidOperationException(
        "the attacker reproduced the reset token from the seed - System.Random is a deterministic PRNG, not a secret");
}

Console.WriteLine("The token was unpredictable.");

static string ResetToken(int seed)
{
    var rng = new Random(seed); // seeded PRNG: the seed fully determines the bytes
    var bytes = new byte[16];
    rng.NextBytes(bytes);
    return Convert.ToHexString(bytes);
}
