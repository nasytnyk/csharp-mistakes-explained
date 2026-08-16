// Exhibit #0078: forgetting Commit lets the `using` roll the transaction back.
//
// A tiny in-memory database with real transaction semantics: writes are staged
// until Commit, and Dispose without Commit rolls them back. The `using` added
// for safety is exactly what discards the work.

var db = new Database();

using (var tx = db.BeginTransaction())
{
    tx.Insert("INV-1001", 149.99m);
    tx.Insert("INV-1002", 200.00m);
    tx.Insert("INV-1003", 75.50m);
    Console.WriteLine($"Staged {tx.Count} invoices.");
    // 💥 forgot tx.Commit() - leaving the block disposes the transaction, which rolls back
}

Console.WriteLine($"Rows persisted in the database: {db.Rows.Count}");

// Self-audit: everything we inserted must be in the database.
if (db.Rows.Count != 3)
    throw new InvalidOperationException(
        $"staged 3 invoices but the database holds {db.Rows.Count}: the `using` block disposed an " +
        "uncommitted transaction, and a transaction's Dispose rolls back - the writes were discarded " +
        "silently, with no exception and a clean exit");

Console.WriteLine("All invoices persisted.");

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
