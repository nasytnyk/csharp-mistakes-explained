// Exhibit #0078 - the fix: commit before the `using` block closes.
//
// One line: tx.Commit() inside the block. Dispose then has nothing to roll
// back, and the staged writes reach the database. The rest is identical to Bad.cs.

var db = new Database();

using (var tx = db.BeginTransaction())
{
    tx.Insert("INV-1001", 149.99m);
    tx.Insert("INV-1002", 200.00m);
    tx.Insert("INV-1003", 75.50m);
    Console.WriteLine($"Staged {tx.Count} invoices.");
    tx.Commit(); // commit while the transaction is still open, before Dispose runs
}

Console.WriteLine($"Rows persisted in the database: {db.Rows.Count}");

if (db.Rows.Count != 3)
    throw new InvalidOperationException(
        $"staged 3 invoices but the database holds {db.Rows.Count}");

Console.WriteLine("All invoices persisted. As it should be.");

sealed class Database
{
    public Dictionary<string, decimal> Rows { get; } = new();
    public Transaction BeginTransaction() => new(this);
}

sealed class Transaction : IDisposable
{
    readonly Database db;
    readonly Dictionary<string, decimal> staged = new();
    bool committed;

    public Transaction(Database db) => this.db = db;
    public int Count => staged.Count;

    public void Insert(string id, decimal amount) => staged[id] = amount;

    public void Commit()
    {
        foreach (var (id, amount) in staged)
            db.Rows[id] = amount;
        committed = true;
    }

    public void Dispose()
    {
        if (!committed)
            staged.Clear(); // roll back: nothing staged ever reaches db.Rows
    }
}
