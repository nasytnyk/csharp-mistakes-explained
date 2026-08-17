// Exhibit #0082: a nullable bool sends "unknown" down the wrong branch.
//
// MarketingConsent is a bool?: true = opted in, false = opted out, null = never
// asked. The send loop mails everyone "not opted out" - and null is not false,
// so the undecided users get mailed too.

var users = new[]
{
    new User("alice@example.com", MarketingConsent: true),   // opted in
    new User("bob@example.com",   MarketingConsent: false),  // opted out
    new User("carol@example.com", MarketingConsent: null),   // never asked - undecided
};

var emailed = new List<string>();
foreach (var user in users)
    if (user.MarketingConsent != false) // 💥 null != false is true, so "undecided" is treated like "opted in"
        emailed.Add(user.Email);

Console.WriteLine($"Emailed {emailed.Count}: {string.Join(", ", emailed)}");

// Self-audit: only users who EXPLICITLY consented may be emailed.
int explicitlyConsented = users.Count(u => u.MarketingConsent == true);
if (emailed.Count != explicitlyConsented)
    throw new InvalidOperationException(
        $"emailed {emailed.Count} users but only {explicitlyConsented} gave explicit consent: a bool? has three " +
        "states, and `!= false` lumps null (never asked) in with true - the undecided users were emailed, a " +
        "consent violation with no error and a clean run");

Console.WriteLine("Only consented users emailed.");

record User(string Email, bool? MarketingConsent);
