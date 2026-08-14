# State

_Snapshot; `dotnet run tools/next-id.cs` is authoritative for numbering._

- Exhibits: **64** | Halls: **25** | Next free id: **0065**
- Last updated after: #0064 (2026-08-13) - missing-accept-header

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
| 0046 | nullability | null-forgiving-lies | 5 |
| 0047 | nullability | the-smuggled-null | 5,6 |
| 0048 | testing | collection-assert-is-ordered | 4 |
| 0049 | records | record-tostring-leaks-secrets | 5 |
| 0050 | numbers | the-widening-that-came-too-late | 4,5 |
| 0051 | pattern-matching | the-banned-user-walked-in | 4,5 |
| 0052 | events | one-handler-kills-the-rest | 5 |
| 0053 | disposal | the-wrapper-that-stole-the-stream | 5 |
| 0054 | value-types | new-guid-is-empty | 4,5 |
| 0055 | numbers | decimal-keeps-its-scale | 4,5 |
| 0056 | nullability | null-poisons-the-sum | 4,5 |
| 0057 | linq | except-silently-dedups | 4,5 |
| 0058 | inheritance | the-override-that-wasnt | 4,5 |
| 0059 | io | read-without-seeking-to-start | 5 |
| 0060 | reflection | changetype-chokes-on-nullable | 4 |
| 0061 | reflection | invoke-wraps-the-exception | 5 |
| 0062 | http | stringcontent-defaults-to-text-plain | 4,5 |
| 0063 | http | disposing-the-response-disposes-its-content | 3,5 |
| 0064 | http | missing-accept-header | 4,5 |

## Halls

**22 opened, 7 planned** (29 total). Full registry (slugs, emoji, status) is in
`halls.md` - taxonomy expanded to ~30 on 2026-07-19; `regex` retired at hall
level 2026-07-24 (see `rejected.md`). This session opened Memory (#0038), Boxing
(#0041), Generics (#0044), Nullability (#0046), Testing (#0048). Security stayed
planned (both picks rejected). Async/Memory/Boxing/Generics/Nullability backlogs
cleared/closed 2026-08; remaining planned halls stocked.

## Infra status

- `tools/next-id.cs` - live (counts folders, flags dup numbers, exit 1).
- `tools/check-links.cs` - live (bare #NNNN refs + dead relative links, exit 1). Run before every exhibit commit.
- `tools/gen-frontpage.cs` - live (regenerates the README front page from
  `halls.md` + exhibit front-matter; credits `@author`). Run before committing
  an exhibit so the front page never goes stale.
- CI (GitHub Actions) - deferred by the curator; see `todo.md`.
