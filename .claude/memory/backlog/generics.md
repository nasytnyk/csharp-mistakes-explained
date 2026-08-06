# 🧬 generics

> Status: **opened** (2026-08-05, by #0044 variance-skips-value-types). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

> Chosen candidate queue exhausted 2026-08-05: 2 shipped (#0044 variance-skips-value-types,
> #0045 sort-compiles-for-anything), 2 rejected (static-field-per-closed-type,
> t-question-mark-is-not-nullable; see `rejected.md`). Two Seeds remain below.

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
