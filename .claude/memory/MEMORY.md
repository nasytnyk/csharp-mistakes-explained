# Memory index

Committed project memory for **C# Mistakes Explained**. This index is
auto-loaded every session (imported from `CLAUDE.md`); the topic files are read
on demand.

**Snapshot (2026-07-24):** 34 exhibits, 17 halls opened / 16 planned, next id
0035. Museum is open to contributors (PR is the gate). Every planned hall is
stocked with verified candidates. `regex` retired at hall level.

- `halls.md` - canonical hall registry (emoji, display name, opened/planned); the front-page generator reads it.
- `state.md` - current exhibit count, the shipped-exhibit table, next id, infra status.
- `backlog/` - candidate exhibits, **one file per hall** (`backlog/<slug>.md`), each with a `## Seeds` tail; format and rules in `backlog/README.md`.
- `rejected.md` - flat list of declined candidates (do not rebuild/re-propose) + retired topics. **Read before proposing.**
- `archetypes.md` - the 7 bug archetypes; the curation-balance taxonomy.
- `todo.md` - remaining framework/infra work (CI deferred; contributor onboarding shipped).

After an exhibit lands: update `state.md` and delete the candidate's block from
its `backlog/<slug>.md`. Memory is committed **separately** from exhibit
commits; the per-hall split lets each hall's additions be their own commit.
