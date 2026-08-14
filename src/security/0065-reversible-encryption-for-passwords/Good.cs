// Exhibit #0065: the fix

using System.Security.Cryptography;
using System.Text;

// A password vault that protects passwords by hashing them (salted PBKDF2) - one-way.
var vault = new PasswordVault();

var stored = vault.Store("hunter2");
Console.WriteLine($"Stored blob: {Convert.ToBase64String(stored)}");

// Login works by hashing the attempt and comparing - no decryption anywhere.
Console.WriteLine($"Login with correct password: {vault.Verify("hunter2", stored)}");

// There is no key and no reverse: a hash cannot be turned back into the password.
var recovered = vault.Recover(stored);
Console.WriteLine($"Recovered from blob: {recovered ?? "(cannot be recovered)"}");

// Self-audit: a leaked store must not yield the password, and login must still work.
if (recovered == "hunter2")
{
    throw new InvalidOperationException("the stored password was recovered to plaintext");
}
if (!vault.Verify("hunter2", stored))
{
    throw new InvalidOperationException("login failed for the correct password");
}

Console.WriteLine("Stored passwords cannot be recovered. As it should be.");

// Stores a password by HASHING it - one-way, per-user salt, slow KDF.
class PasswordVault
{
    private const int SaltLen = 16, Iterations = 100_000, HashLen = 32;

    public byte[] Store(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLen);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashLen);
        return [.. salt, .. hash]; // salt + hash
    }

    public bool Verify(string password, byte[] stored)
    {
        byte[] salt = stored[..SaltLen];
        byte[] expected = stored[SaltLen..];
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashLen);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public string? Recover(byte[] stored) => null; // a hash cannot be inverted
}
