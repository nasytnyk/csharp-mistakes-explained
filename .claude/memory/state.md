# State

_Snapshot; `dotnet run tools/next-id.cs` is authoritative for numbering._

- Exhibits: **45** | Halls: **20** | Next free id: **0046**
- Last updated after: #0045 (2026-08-05) - sort-compiles-for-anything

## Exhibits shipped

| id | hall | slug | archetype |
|--:|------|------|:--:|
| 0001 | collections | modify-while-enumerating | 2 |
| 0002 | numbers | doubles-for-money | 4 |
| 0003 | async | race-on-shared-counter | 6 |
| 0004 | collections | dictionary-key-mutation | 2 |
| 0005 | exceptions | throw-ex-stack-amnesia | 7 |
| 0006 | linq | closure-over-loop-variable | 1 |
| 0007 | async | async-void | 1 |
| 0008 | orm | n-plus-one | - |
| 0009 | linq | multiple-enumeration | 1,3 |
| 0010 | events | immortal-subscriber | 6 |
| 0011 | value-types | defensive-copy-ambush | 3 |
| 0012 | serialization | zero-priced-order | 4,5 |
| 0013 | linq | distinct-that-didnt | 2,4 |
| 0014 | di-lifetimes | container-hoarder | 5 |
| 0015 | exceptions | cancellation-eaten-by-catch | 5 |
| 0016 | async | token-tourism | 5 |
| 0017 | exceptions | finally-that-lied | 5,7 |
| 0018 | async | tasks-are-not-results | 1,5 |
| 0019 | async | forgotten-task | 1,5 |
| 0020 | datetime | shrinking-billing-day | 5 |
| 0021 | async | whenall-hides-exceptions | 5 |
| 0022 | di-lifetimes | the-captive-scoped | 6 |
| 0023 | events | unremovable-lambda | 2 |
| 0024 | serialization | polymorphic-loses-derived | 4,5 |
| 0025 | numbers | math-round-banker | 4 |
| 0026 | disposal | dispose-what-you-dont-own | 5 |
| 0027 | equality | null-comparisons-are-always-false | 4,5 |
| 0028 | records | with-copies-the-reference | 3 |
| 0029 | numbers | nan-poisons-comparison | 4 |
| 0030 | collections | array-covariance-betrayal | 4 |
| 0031 | async | parallel-foreach-swallows-async | 1,5 |
| 0032 | logging | interpolated-log-loses-everything | 4 |
| 0033 | pattern-matching | switch-expression-not-exhaustive | 5 |
| 0034 | inheritance | virtual-call-in-constructor | 1 |
| 0035 | async | the-pool-that-ate-itself | 5,6 |
| 0036 | async | the-eliminated-await | 1,5 |
| 0037 | async | the-double-wrapped-task | 4,1 |
| 0038 | memory | the-closure-that-held-everything | 6 |
| 0039 | memory | the-stack-that-only-grows | 6 |
| 0040 | memory | the-span-left-behind | 3 |
| 0041 | boxing | unbox-must-match-exact-type | 4,5 |
| 0042 | boxing | boxed-values-are-equal-not-same | 2,4 |
| 0043 | boxing | nullable-boxes-to-nothing | 4,5 |
| 0044 | generics | variance-skips-value-types | 4,5 |
| 0045 | generics | sort-compiles-for-anything | 5 |

## Halls

**20 opened, 9 planned** (29 total). Full registry (slugs, emoji, status) is in
`halls.md` - taxonomy expanded to ~30 on 2026-07-19; `regex` retired at hall
level 2026-07-24 (see `rejected.md`); Memory opened 2026-08-05 by #0038, Boxing
by #0041, Generics by #0044. Async, Memory, and Boxing backlogs cleared 2026-08-05
(see `rejected.md`); other planned halls remain stocked.

## Infra status

- `tools/next-id.cs` - live (counts folders, flags dup numbers, exit 1).
- `tools/check-links.cs` - live (bare #NNNN refs + dead relative links, exit 1). Run before every exhibit commit.
- `tools/gen-frontpage.cs` - live (regenerates the README front page from
  `halls.md` + exhibit front-matter; credits `@author`). Run before committing
  an exhibit so the front page never goes stale.
- CI (GitHub Actions) - deferred by the curator; see `todo.md`.
