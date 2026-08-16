---
id: "0078"
title: the transaction that rolled itself back
category: disposal
tags: [disposal, transactions, using]
rule: "always `Commit` before the `using` block closes - a transaction's `Dispose` **rolls back**"
---

# #0078 - The Transaction That Rolled Itself Back

## 💥 Symptom

The writes ran, no exception was thrown, the process exited `0` - and the rows
are not in the database. The insert code is right there, the logs say it
executed, yet reconciliation comes up short. The one thing that looks *safest* in
the method - the `using` you wrapped the transaction in so it could never leak -
is the thing that threw the work away.

## 🔍 The Offending Code

```csharp
using (var tx = db.BeginTransaction())
{
    tx.Insert("INV-1001", 149.99m);
    tx.Insert("INV-1002", 200.00m);
    // ... forgot tx.Commit()
} // 💥 leaving the block disposes an uncommitted transaction, which rolls back
```

## 🧠 What's Actually Going On

A transaction's contract is *commit or roll back* - and `Dispose` has to pick one
for you, because leaving a transaction open would hold locks forever. Every
`IDbTransaction` (ADO.NET, EF Core, Dapper) resolves an un-committed transaction
the only safe way it can: it **rolls back**. So the closing brace of your `using`
does not merely release a handle - it makes a decision about your data, and the
decision for "you never called `Commit`" is "discard everything."

The broken belief is "`using` guarantees cleanup, so wrapping the transaction is
the safe thing to do." Wrapping it *is* correct - a transaction must be disposed.
The trap is that for a transaction, disposal is not neutral cleanup like closing a
file; it is a semantic rollback. Miss the `Commit` and the safety construct you
added becomes a silent `DELETE` of everything the block did - no error, no log, a
clean exit, and the loss only surfaces when someone reads the data back.

## ✅ The Fix

Commit inside the block, while the transaction is still open, so `Dispose` has
nothing left to undo.

```csharp
using (var tx = db.BeginTransaction())
{
    tx.Insert("INV-1001", 149.99m);
    tx.Insert("INV-1002", 200.00m);
    tx.Commit(); // commit before the block closes; Dispose is now a no-op
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| `Commit()` as the last statement in the block | The normal path - stage the work, commit, then let `Dispose` release the (now-committed) transaction. |
| Commit-or-throw, never fall off the end | Any block with early `return`/`continue`/branches - make sure *every* path either commits or deliberately throws, so no path exits the block having done work without committing. |
| A helper that commits on success, rolls back on exception | Repeated across a codebase - `db.InTransaction(tx => { ... })` that commits when the delegate returns normally and rolls back on exception, so callers cannot forget. |
| `TransactionScope` with `scope.Complete()` | Ambient/`System.Transactions` code - same shape, same trap: forget `Complete()` and disposing the scope rolls back. |

## 😈 The Even Worse Sibling

Here the rollback at least undoes a *complete* unit of work - you lose all three
inserts together, which is ugly but consistent. The nastier version is a block
that commits on some paths and not others: an early `return` after the first
insert but before `Commit`, or a `continue` in a loop that skips the commit for
certain rows, leaves you with a *partial* write history where some transactions
committed and some silently rolled back on the same run - the ledger is now
internally inconsistent, and because nothing errored, there is no failed-request
metric pointing at the gap. And the mirror image bites too: a transaction you
forget to *dispose at all* does the opposite harm - it holds its locks open,
blocking every other writer until the connection is reclaimed.

## 🎓 Advanced Nuance

- **Rollback-on-dispose is the documented behavior, not an accident.** ADO.NET's
  `IDbTransaction`, EF Core's `IDbContextTransaction`, and `TransactionScope` all
  specify that disposing without committing rolls back. It is the safe default -
  the bug is relying on the safe default to *persist* your data.
- **`SaveChanges` is not `Commit`.** In EF Core, `SaveChanges()` inside an
  explicit `using var tx = ctx.Database.BeginTransaction()` writes the rows but
  does not commit the transaction; without `tx.Commit()` the dispose still rolls
  the whole thing back, even though `SaveChanges` reported success and returned a
  row count.
- **An exception path *should* roll back - that is the feature.** The goal is not
  "never roll back"; it is "roll back only when you meant to." The fix makes the
  *success* path commit explicitly, leaving `Dispose` to roll back exactly the
  paths that threw - which is what a transaction is for.

## 🔎 How to Find It in Your Codebase

- Grep for `BeginTransaction` and `TransactionScope`, then check that every
  resulting `using` block contains a `Commit()` / `Complete()` on its success
  path - and that no early `return`/`break`/`continue` jumps past it.
- Watch for blocks that call `SaveChanges()` (EF Core) inside an explicit
  transaction `using` but never `tx.Commit()` - the rows appear to save, then
  vanish on dispose.
- Symptom-side: writes that "don't stick," reconciliation shortfalls with no
  errors in the logs, data present in application memory during the request but
  absent afterward, and bugs that disappear the moment you add a `Commit` you
  thought was redundant.
- Prefer a transaction helper (`InTransaction(...)` / a repository method) that
  commits on normal return and rolls back on exception, so individual call sites
  cannot forget the commit at all.
