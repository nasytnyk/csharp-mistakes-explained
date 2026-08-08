// Exhibit #0054: the fix

// The same billing run - but the fee is `virtual` in the base and `override` in the
// derived class, so dispatch follows the object, not the type of the reference.

BasicAccount[] accounts =
{
    new BasicAccount("free-tier"),
    new PremiumAccount("premium-tier"), // a premium account, referenced as BasicAccount
};

decimal charged = 0m;
foreach (BasicAccount account in accounts)
{
    decimal fee = account.MonthlyFee(); // virtual dispatch -> the object's own fee, base reference or not
    Console.WriteLine($"{account.Name}: charged ${fee}");
    charged += fee;
}

// Self-audit: free owes 5, premium owes 20.
const decimal owed = 5m + 20m;
if (charged != owed)
{
    throw new InvalidOperationException(
        $"billed ${charged} but owed ${owed}");
}

Console.WriteLine("Every account billed its true fee. As it should be.");

class BasicAccount
{
    public string Name { get; }
    public BasicAccount(string name) => Name = name;
    public virtual decimal MonthlyFee() => 5m;
}

class PremiumAccount : BasicAccount
{
    public PremiumAccount(string name) : base(name) { }
    public override decimal MonthlyFee() => 20m; // override: dispatched by the object's runtime type
}
