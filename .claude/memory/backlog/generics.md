# 🧬 generics

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### variance-skips-value-types (A4,5)

- **Twist:** a List&lt;string&gt; IS an IEnumerable&lt;object&gt;; a
  List&lt;int&gt; is NOT - covariance covers reference types only, so the
  same `is` check answers opposite for the two most ordinary lists in the
  codebase.
- **Mechanic:** `out T` variance is legal only for reference conversions:
  boxing changes representation, so int-to-object does not qualify.
  List&lt;string&gt; passes `is IEnumerable<object>`, List&lt;int&gt;
  fails it; string[] and int[] split identically. Nothing at the call
  site hints at the reference-types-only rule. The fix is the non-generic
  `System.Collections.IEnumerable`, which every List&lt;T&gt; implements.
- **Who hits it:** "iterate anything" plumbing - logging enrichers,
  exporters, message handlers probing object payloads with
  `is IEnumerable<object>` to enumerate contents. Text and DTO
  collections take the enumeration path; int/decimal/DateTime collections
  silently take the fallback.
- **Repro:** two payloads (List&lt;string&gt;, List&lt;int&gt;):
  true/false on the `is` check, same for the arrays; a PrintAll pipeline
  enumerates the letters and reports the numbers as "not a sequence".
  Deterministic, no packages.
- **Damage:** value-type collections routed to the scalar path - exports
  where numeric columns vanish while text columns flow, logs printing
  ``List`1[System.Int32]`` as a single value - all silent.
- **😈 seed:** it passes review and CI wherever the test payloads are
  string-shaped - which is nearly everywhere, since humans write example
  data in words; the first integer batch in production is the reveal.
- **Verified:** ran on .NET 10 (2026-07-24): true/false for the lists,
  true/false for the arrays, pipeline enumerated strings and skipped
  ints.

### sort-compiles-for-anything (A5)

- **Twist:** `list.Sort()` compiles for every T and throws at runtime for
  types with no ordering - *including records* - and a one-element list
  does not throw, so small test data certifies code that dies on the
  first real batch.
- **Mechanic:** Sort and OrderBy carry no IComparable constraint;
  Comparer&lt;T&gt;.Default resolves at runtime and fails on the first
  actual comparison ("Failed to compare two elements", inner
  ArgumentException). Zero- and one-element sorts perform no comparisons
  and pass (verified). Records generate Equals/GetHashCode but NOT
  CompareTo - value semantics stop at equality - so record keys throw
  too, against everyone's "records just handle this" instinct.
- **Who hits it:** ordering by DTOs and record keys:
  `pairs.OrderBy(p => p.Key)` with a record key compiles, ships, and
  throws at the first enumeration that compares two rows.
- **Repro:** List&lt;Widget&gt;.Sort() throws with inner
  ArgumentException; the single-element list passes; a record list throws
  the same; OrderBy(w =&gt; w).ToList() and OrderBy(p =&gt; p.Key) with a
  record key both throw. Deterministic, no packages.
- **Damage:** a crash triggered by dataset *size*, not code: dev and unit
  tests with one row stay green, the two-row batch in staging goes red -
  and the stack points into sort internals, not at the record that never
  had an ordering.
- **😈 seed:** records are the accelerant: they hand you ==, Equals, and
  GetHashCode for free, so assuming comparability came in the same gift
  box is nearly reasonable - equality was generated, ordering wasn't.
- **Verified:** ran on .NET 10 (2026-07-24): widgets and records threw
  (inner ArgumentException), single-element Sort passed, both OrderBy
  variants threw at enumeration.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **generics:** a static constructor in `Registry<T>` "runs once" but
  actually runs once *per closed type* - the per-closed-type statics model
  (static-field-per-closed-type was rejected 2026-08-05, see `rejected.md`).
  Only promote if reframed with a fresh twist and damage.

- **backtick-names-collide** - typeof(List&lt;int&gt;).Name and
  typeof(List&lt;string&gt;).Name are both ``List`1`` (verified
  2026-07-24): type-name-keyed routing, caching, and metrics collapse
  every closed generic of one definition into a single bucket. Needs a
  damage framing (message-type headers?) before promoting.
