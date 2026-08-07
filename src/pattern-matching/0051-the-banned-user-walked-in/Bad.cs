// Exhibit #0051: the banned user walked in

// A moderation gate blocks banned users. It uses the modern pattern syntax an
// IDE hint suggested - `is not Access.Banned` - in place of a HasFlag check.

// This account was banned, then later also muted - two flags set now.
Access access = Access.Banned | Access.Muted;

// The gate: let everyone in except the banned. Reads perfectly in English.
bool allowed = access is not Access.Banned; // 💥 a constant pattern is exact equality, not HasFlag

Console.WriteLine($"User access: {access}");
Console.WriteLine(allowed ? "Access granted." : "Access denied - user is banned.");

// Self-audit: an account carrying the Banned bit must never be let in.
if (allowed && access.HasFlag(Access.Banned))
{
    throw new InvalidOperationException(
        $"a banned user ({access}) was granted access - `is not Access.Banned` is exact equality, so the " +
        "combined value Banned|Muted (3) is not equal to the lone Banned constant (1) and slips past the gate");
}

Console.WriteLine("Gate holds: no banned user got in.");

[Flags]
enum Access { None = 0, Banned = 1, Muted = 2, Verified = 4 }
