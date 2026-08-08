// Exhibit #0054: `new` hides, it does not override

// A billing run charges every account its monthly fee. PremiumAccount defines its own,
// higher fee - but marks it `new`, not `override`. The accounts are held by their base type.

BasicAccount[] accounts =
{
    new BasicAccount("free-tier"),
    new PremiumAccount("premium-tier"), // a premium account, referenced as BasicAccount
};

decimal charged = 0m;
foreach (BasicAccount account in accounts)
{
    decimal fee = account.MonthlyFee(); // 💥 static type is BasicAccount -> base fee, even for the premium one
    Console.WriteLine($"{account.Name}: charged ${fee}");
    charged += fee;
}

// Self-audit: free owes 5, premium owes 20.
const decimal owed = 5m + 20m;
if (charged != owed)
{
    throw new InvalidOperationException(
        $"billed ${charged} but owed ${owed} - PremiumAccount.MonthlyFee is marked `new`, which HIDES rather " +
        "than overrides, so the call through the BasicAccount reference ran the base fee for the premium account");
}

Console.WriteLine("Every account billed its true fee.");

class BasicAccount
{
    public string Name { get; }
    public BasicAccount(string name) => Name = name;
    public decimal MonthlyFee() => 5m;
}

class PremiumAccount : BasicAccount
{
    public PremiumAccount(string name) : base(name) { }
    public new decimal MonthlyFee() => 20m; // `new`: hides the base method, does not override it
}
