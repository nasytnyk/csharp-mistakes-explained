// Exhibit #0051: the fix

// The same moderation gate - but it asks the bitwise question a pattern cannot:
// is the Banned bit set? HasFlag tests the bit, not whole-value equality.

// This account was banned, then later also muted - two flags set now.
Access access = Access.Banned | Access.Muted;

// The gate: let everyone in except the banned. Now it checks the flag, not the value.
bool allowed = !access.HasFlag(Access.Banned); // the bitwise question: is the Banned bit set?

Console.WriteLine($"User access: {access}");
Console.WriteLine(allowed ? "Access granted." : "Access denied - user is banned.");

// Self-audit: an account carrying the Banned bit must never be let in.
if (allowed && access.HasFlag(Access.Banned))
{
    throw new InvalidOperationException(
        $"a banned user ({access}) was granted access");
}

Console.WriteLine("Gate holds: no banned user got in. As it should be.");

[Flags]
enum Access { None = 0, Banned = 1, Muted = 2, Verified = 4 }
