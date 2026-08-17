// Exhibit #0082 - the fix: gate on the affirmative state, not the negative one.
//
// `== true` is true for exactly one of the three states, so null (undecided)
// falls on the safe side. The rest is identical to Bad.cs.

var users = new[]
{
    new User("alice@example.com", MarketingConsent: true),   // opted in
    new User("bob@example.com",   MarketingConsent: false),  // opted out
    new User("carol@example.com", MarketingConsent: null),   // never asked - undecided
};

var emailed = new List<string>();
foreach (var user in users)
    if (user.MarketingConsent == true) // only an explicit yes qualifies; null and false do not
        emailed.Add(user.Email);

Console.WriteLine($"Emailed {emailed.Count}: {string.Join(", ", emailed)}");

int explicitlyConsented = users.Count(u => u.MarketingConsent == true);
if (emailed.Count != explicitlyConsented)
    throw new InvalidOperationException(
        $"emailed {emailed.Count} users but only {explicitlyConsented} gave explicit consent");

Console.WriteLine("Only consented users emailed. As it should be.");

record User(string Email, bool? MarketingConsent);
