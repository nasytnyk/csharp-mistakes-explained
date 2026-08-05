# Rejected

What the curator declined, and WHY - so I learn his taste and stop
re-proposing. Add a row whenever he turns something down. Watch for patterns
in the "reason" column; they encode the curation bar.

| candidate | reason category | detail |
|-----------|-----------------|--------|
| turkish-i-login | too banal / primer-level | everyone typed a password in the wrong keyboard layout as a kid - common knowledge from the moment you touch a computer, below the museum's floor. |
| int-overflow-in-cart | too banal / primer-level | every junior has already hit integer overflow; it's a primer, not an exhibit - people know it before they're paid to code. |
| path-combine-betrayal | vacuum example / no who-where-how | you can't say who attacks, from where, or how - a vacuum scenario with no reproducible real-world context, so not interesting. Built and reverted at #0021 before commit. |
| .Result deadlock | cannot reproduce honestly | the *SynchronizationContext* form (UI/legacy ASP.NET) needs a captured context a console app can't show. Rule: no exhibit that doesn't reproduce. NOTE: the *thread-pool starvation* form is a different mechanic (no context, deterministic via a pinned pool) and shipped as #0035 the-pool-that-ate-itself - this row bans only the SyncContext variant. |
| StringBuilder-in-a-loop | proven only by timing | "trust me it's slow" is banned; timings flicker across machines. |
| quadratic ElementAt | proven only by timing | same. |
| culture/timezone bug w/o pinning | CI would lie | if the code doesn't pin culture/zone, the demo's outcome depends on the runner. Only ship if the code fixes the environment explicitly. |
| datetime-kind-round-trip | premise doesn't hold | my error again: System.Text.Json round-trips DateTime correctly for all three Kinds - value, Kind and instant all survive (verified). The real versions need Newtonsoft's default DateTimeZoneHandling, a database column with no offset, or two machines in different zones - none demonstrable deterministically in one console file. Rejected before any code was written. |
| enum-accepts-undefined | doesn't happen in real code | "in real life nobody pushes a number into an enum." The mechanic is real and non-obvious (TryParse rejects an invalid name but accepts an invalid number, so the usual validation gate is not one), but the curator does not see numeric junk arriving at an enum boundary in practice. Built and reverted at #0029 before commit. |
| lock-on-a-string | doesn't happen in real code | "nobody in their right mind locks on a string when every tutorial screams to lock on a dedicated object." The literal-string version is textbook-warning material, not production code. The realistic cousin (lock on a runtime string, which locks nothing because runtime strings are not interned) was offered too and also declined. |
| sort-is-unstable | no real damage | "in a normal system this practically never breaks anything." Swapping two elements that compare equal loses nothing you asked to preserve; the mechanic is real (List.Sort is stable up to 16 elements, unstable from 17) but the consequence isn't worth an exhibit. |
| firstordefault-on-structs | premise doesn't hold | "nobody null-checks a struct - nonsense." He is right, and stronger than that: `found == null` does not even compile for a non-nullable struct, so the bug as I framed it cannot happen. My framing dressed a real nuance (default(T) is zeros, not null) in an impossible scenario. |
| regex (entire hall) | not a footgun domain | hall-level removal, his words (translated from Ukrainian): "it's not the kind of section where you can easily screw up." Regex mistakes don't meet the museum's easy-to-make bar, so the whole topic is out: hall row deleted from halls.md, backlog/regex.md deleted with all four queued candidates (missing-anchors-pass-anything, dot-misses-newline, unescaped-regex-input, slash-d-matches-unicode-digits). Never propose regex candidates again. |
| the-overlapping-timer | topic out of scope (curator) | his words: "timer не використовуємо в музеї" (we don't use Timer in the museum). System.Threading.Timer / recurring-timer bugs are out by his preference, real or not - a topic call, not a flaw in this candidate. Extends to the still-queued the-collected-timer (the other Timer candidate); do not propose Timer exhibits. |
| semaphore-never-released | curator's discretion (no reason) | his words: "просто так, не хочу його розбирати" (no particular reason, doesn't want to build it). Discretionary and specific to THIS candidate - NOT a rejection of SemaphoreSlim as a topic (the-self-deadlock stays valid). No pattern to generalize. |

## Reason categories (the bar, distilled)

1. **Predictable finale / primer-level** - either the reader guesses the outcome from the title, OR the bug is so universally experienced (integer overflow, wrong keyboard layout) that it's common knowledge from day one - a primer, below the museum's floor. An exhibit needs a mechanic twist AND a topic that isn't already in everyone's bones. When proposing, pre-filter anything a person learns "the moment they touch a computer."
2. **Cannot reproduce honestly** - won't fail deterministically in a single console file.
3. **Proven only by timing** - performance claim with no hard assertion; banned.
4. **CI would lie** - outcome depends on machine culture/zone/GC without the code pinning it.
5. **Doesn't happen in real code** - technically real, but the author judges it too contrived to occur in actual projects. His lived-experience filter overrides textbook correctness; when unsure whether a bug is "real enough", prefer everyday-contract scenarios over exotic API footguns.
6. **Premise doesn't hold** - my own error: the proposed scenario cannot occur as described (the compiler forbids it, the API prevents it). Before proposing, mentally compile the bad code - if it wouldn't build, the exhibit doesn't exist.
7. **No real damage** - the mechanic is real and does occur, but in a normal system nothing meaningful breaks. Reproducing a quirk is not enough; ask what the reader actually loses. If the honest answer is "nothing you asked to keep", it isn't an exhibit.
8. **Not a footgun domain (hall-level)** - the curator can retire an entire topic area: the domain does not generate the everyday, easy-to-make mistakes the museum collects. All of the hall's candidates are rejected at once, the hall row leaves halls.md, and its backlog file is deleted. Check new hall proposals against this bar before stocking them.
9. **Topic out of scope (curator)** - a mechanic or API family the curator keeps out of the museum by preference, even when the bug is real (e.g. System.Threading.Timer). Rejects every candidate in that family, not only the named one; pre-filter the whole topic, not just the one slug.
10. **Curator's discretion (no reason)** - he passed with no analytical objection ("просто так"). Specific to that one candidate; do not generalize to its topic or infer a taste pattern from it.

If a new idea trips any of these, pre-filter it before proposing.
