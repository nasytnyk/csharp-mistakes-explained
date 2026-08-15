---
description: How to write an exhibit README - structure, front-matter, cross-links, tone. Auto-loads when editing an exhibit README.
paths:
  - "src/**/README.md"
---

# Exhibit README conventions

## Front-matter (YAML, first thing in the file)

```yaml
---
id: "0009"                 # quoted string, keeps leading zeros
title: Enumerating a LINQ query twice
category: linq             # == the hall folder name
tags: [LINQ, IEnumerable, deferred-execution]
rule: "never enumerate a LINQ query twice - materialize it once"
---
```

- `rule` is the exhibit's commandment - lowercase `never ...`. It is the **only**
  text the front page shows for this exhibit, so it has to stand alone.
- **Keep it as short as an axiom can be.** Fewer words is better - a commandment,
  not an explanation. Prefer `never <do X> - <the short fix>`, and cut any trailing
  "because/how" clause once the rule stands on its own. If it reads like a sentence,
  tighten it until it reads like a law. (The curator trims these aggressively; write
  them pre-trimmed.)
- `author: github-username` - **only on contributed exhibits.** The generator
  renders it as `(@username)` after the rule. The curator's own exhibits omit
  the field and stay uncredited, so a contributor's name stands out.
- **Emphasize the key words inside `rule`** so the front page is scannable:
  `**bold**` for the domain concept (the thing the reader scans for), backticks
  for real C# identifiers. One or two emphasized spans - not a bold soup.
  Example: ``never mutate an object that serves as a **dictionary key**`` and
  ``never write `async void` outside **event handlers**``.

## Filenames and translations

- The exhibit page is `README.md` (or `README-en.md`) - either is the **canonical**
  English page the front page uses. An optional `README-ua.md` (or another
  `README-<lang>.md`) holds a translation alongside it: same section order, same
  links, and translate the front-matter `title` and `rule` too - but keep `id`,
  `category`, and `tags` unchanged. `gen-frontpage` lists the English canonical
  once (translations are skipped); `check-links` validates every `README*.md`.

## Section order (fixed)

1. `# #NNNN - Title` (this H1 keeps the bare `#NNNN` form)
2. `## 💥 Symptom` - the production pain, not theory. Make the reader say
   "oh, I've seen this."
3. `## 🔍 The Offending Code` - the minimal incriminating snippet. No
   "Reproduce: dotnet run" block - the run command is obvious.
4. `## 🧠 What's Actually Going On` - the mechanic. The educational core.
5. `## ✅ The Fix` - idiomatic fix + `[Good.cs](Good.cs)` + a "when is this the
   right call" table.
6. `## 😈 The Even Worse Sibling` *(optional, strongly preferred)* - the
   silent/nastier variant. Recurring punchline: "the crash in this exhibit is
   the *lucky* outcome."
7. `## 🎓 Advanced Nuance` *(optional, strongly preferred)* - the twist that
   surprises even people who know the bug: version history, an edge case, a myth
   to bust. These two optional sections add depth for the reader who wants to go
   deeper - include them when you can. ("Advanced" describes the material, not a
   caste of person - there are no "seniors" here.)
8. `## 🔎 How to Find It in Your Codebase` - grep patterns, analyzer IDs
   (CA2200, VSTHRD100), IDE inspections, `.editorconfig` recipes.

**This is the LAST section.** No "Dig Deeper" or external-link section after it
- nobody opens them.

## Cross-references

Reference another exhibit as a clickable link, never a bare `#NNNN` (the number
alone is unsearchable by hand):

```
[0010-immortal-subscriber](../../events/0010-immortal-subscriber/)
```

The `../../<hall>/<NNNN-slug>/` form works from any exhibit README. Non-exhibit
numbers in prose (order #1002, prices) stay bare. `tools/check-links.cs`
enforces this.

## Tone

Restrained "lab / museum" voice. Jokes and punchlines are saved for LinkedIn,
not the repo. The 💥 / 😈 / 🎓 section markers carry the personality; the prose
stays informative. **No shaming** - exhibits mock the code, never the author;
we all wrote this at some point.

## Front page

The root README's exhibit list is **generated** - do not hand-edit it. After the
exhibit exists, run `dotnet run tools/gen-frontpage.cs`; it rebuilds the
per-hall lists and the stats line from `halls.md` and each exhibit's
front-matter (`id`, `category`, `rule`).

Each line is the number as a link, then the commandment - no folder name, no
title:

```
- [0004](src/collections/0004-dictionary-key-mutation/) never mutate an object that serves as a **dictionary key**
```

The front page stays minimal: manifesto, stats, the generated lists, nothing
else - no "How to Run", no "Contributing", no disclaimer.
