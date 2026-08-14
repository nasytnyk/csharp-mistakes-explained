// Exhibit #0065: encrypting passwords instead of hashing them

using System.Security.Cryptography;
using System.Text;

// A password vault that "protects" passwords by encrypting them with the app's key.
var vault = new PasswordVault(key: "app-secret-key-from-config");

var stored = vault.Store("hunter2");
Console.WriteLine($"Stored blob: {Convert.ToBase64String(stored)}");

// Login works by decrypting the stored value and comparing.
Console.WriteLine($"Login with correct password: {vault.Verify("hunter2", stored)}");

// 💥 The same key the app holds turns every stored blob back into plaintext.
var recovered = vault.Recover(stored);
Console.WriteLine($"Recovered from blob + key: {recovered}");

// Self-audit: a leaked store (plus the config that holds the key) must not yield the password.
if (recovered == "hunter2")
{
    throw new InvalidOperationException(
        "the stored password decrypted back to 'hunter2' - encryption is reversible, so anyone with the key " +
        "(which lives in the same config and backups) recovers every password; passwords must be hashed, not encrypted");
}

Console.WriteLine("Stored passwords cannot be recovered.");

// Stores a password by ENCRYPTING it - reversible with the key.
class PasswordVault(string key)
{
    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes(key));

    public byte[] Store(string password)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        byte[] cipher = aes.EncryptCbc(Encoding.UTF8.GetBytes(password), aes.IV);
        return [.. aes.IV, .. cipher]; // IV + ciphertext
    }

    public bool Verify(string password, byte[] stored) => Recover(stored) == password;

    public string Recover(byte[] stored)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        return Encoding.UTF8.GetString(aes.DecryptCbc(stored[16..], stored[..16]));
    }
}
