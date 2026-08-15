# TODO

Remaining framework/infra work. Consolidated 2026-07-24.

## Open

- **CI (GitHub Actions).** *Deferred by the curator (2026-07-19) - he will raise
  it again; do not start unprompted.* On push/PR: build/run every exhibit so they
  don't rot on SDK bumps; run `next-id.cs`, `check-links.cs`, and
  `gen-frontpage.cs` (fail if the front page is stale) - all three exit 1 on
  failure. Package exhibits (EF, DI, STJ) need restore; watch CI time.
- **Launch polish.** Badges (exhibit count), final proofread, LinkedIn poll copy
  (<=30 chars, 4 options).
- **Tags cross-index.** Once tags are consistent across exhibits, generate a
  tag/archetype index alongside the front page (the `archetypes.md` mix and each
  exhibit's front-matter `tags` are the inputs).

## Done

- **Framework migration (2026-07-19).** Native Claude Code mechanisms: root
  `CLAUDE.md`, path-scoped `.claude/rules/`, the `add-exhibit` /
  `propose-exhibits` / `reject-exhibit` skills. Retired the homemade
  `conventions.md`, `exhibit-recipe.md`, `playbook.md`. Memory relocated from
  `claude-calibration/` into `.claude/memory/`, indexed by `MEMORY.md` and
  auto-loaded via a `CLAUDE.md` import.
- **Tools.** `next-id.cs`, `check-links.cs`, `gen-frontpage.cs` (front page is
  generated, list-style, no difficulty levels).
- **Hall taxonomy.** Expanded to ~30 planned halls in `halls.md`; `regex` later
  retired at hall level (2026-07-24, see `rejected.md`).
- **Opened the museum to contributors (shipped 2026-07-21..23).** The curator's
  2026-07-19 direction is live: outsiders fork, run Claude, get the badged menu,
  build to a PR, and land credited on the front page. What shipped -
  - `CONTRIBUTING.md` (thin; the `contribute` skill does the teaching) reversed
    the old solo-project stance;
  - the `contribute` skill onboards, shows the menu, builds, walks to the PR;
  - `author` front-matter + `gen-frontpage.cs` appends `(@username)` after the
    rule (the curator's own exhibits stay uncredited);
  - **PR is the gate** - the curator reviews every exhibit, rejections keep
    feeding `rejected.md`;
  - **number collisions** resolved by *first merged PR wins, second renumbers at
    merge* (precedent: two `#0032` PRs, one became `#0033`).
  First contributor exhibits: #0030 (@tygronia), #0031, #0032 (@helga-pawlowska),
  #0033 (@Archikrim), #0034 (@alejandro-capel).
- **Backlog fully stocked (2026-07-22..24).** Every planned hall carries verified
  candidates; each premise was run on .NET 10 before landing. See per-hall files
  under `backlog/`.
- Exhibits 0001-0034.

## Not yet started

- **Front-page link should open the exhibit folder with a proper landing.** The
  generated rows already point at the folder (`src/<hall>/<NNNN-slug>/`) rather
  than a child README - which is the goal - but since the bilingual switch
  (README-en.md / README-ua.md, no plain README.md) GitHub no longer
  auto-renders a README in the folder view, so the link lands on a bare file
  list. Decide the landing: e.g. have `gen-frontpage` emit a tiny stub
  `README.md` per exhibit folder that just links to the `-en` / `-ua` pages (a
  language chooser that renders), or otherwise make the folder show a rendered
  page. (Curator ask, 2026-08-15.)
- **Claiming / "taken" state in the backlog.** So two contributors don't build
  the same candidate. Not yet needed at current contributor volume; revisit if
  collisions become common.
- **Game layer.** Opening one of the remaining planned halls as the rare
  achievement; the commandment list doubling as a scoreboard. Aspirational.
