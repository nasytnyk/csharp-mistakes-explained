# 🔗 linq

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### average-ignores-the-nulls (A4,5)

- **Twist:** Average over a nullable column divides by the non-null count, not
  the row count - the more data goes missing, the better the metric looks.
- **Mechanic:** `Enumerable.Average(IEnumerable<int?>)` skips nulls entirely:
  {10, null, 20} averages to 15, not 10. Overload resolution picks the
  nullable version silently because the element type is `int?` - the code
  reads identically to the non-nullable case. On an all-null sequence it
  returns null (while the non-nullable overload on an empty sequence throws) -
  so the failure modes differ too.
- **Who hits it:** any report over a database column that allows NULL -
  average rating, average response time - where the business reads "average"
  as covering all rows, but null rows quietly leave the denominator.
- **Repro:** one `int?[]`; show `Average() == 15` while the intended
  per-row average is 10; then an all-null array where the KPI comes back null
  instead of raising any flag. Deterministic, no packages.
- **Damage:** KPIs that *improve* as data collection breaks - the dashboard
  rewards the outage. Sum has the same skip rule, so Sum/Count cross-checks
  disagree with Average on the same table.
- **😈 seed:** same business question, two column types: non-nullable empty
  crashes loudly, nullable all-null returns a polite null - the silent one is
  the production one.
- **Verified:** ran on .NET 10 (2026-07-22): Average of {10, null, 20} == 15.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **linq / collections:** GroupBy on reference-equality keys (every item its
  own group) - real, but the damage is loud, not silent; needs a framing
  where it stays wrong quietly before it clears the bar.

- **zip-drops-the-tail** (A5) - Zip stops at the shorter sequence: pair 100
  ids with 99 names and you silently get 99 rows, no error, the last record
  simply gone.

- **distinctby-keeps-the-wrong-one** (A5) - `DistinctBy(x => x.Id)` keeps the
  *first* row per key; feed it events oldest-first and every duplicate
  resolves to the stale version.

- **linq:** OrderBy with a non-deterministic key, and Single vs First
  surprising on duplicate data - both need a deterministic silent-damage
  framing before promoting.
