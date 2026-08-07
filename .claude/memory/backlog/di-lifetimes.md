# 💉 di-lifetimes

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-silent-override (A4)

- **Twist:** Two registrations of one interface are both legal; the last one
  wins silently - the handler you spent an hour debugging never resolved at
  all.
- **Mechanic:** MS.DI accepts duplicate registrations: single resolution
  (`GetRequiredService<T>`) returns the *last* registration;
  `GetServices<T>` returns all of them. Which implementation runs is decided
  by registration order across Program.cs and every AddXyz extension - a
  global, invisible, order-sensitive contract.
- **Who hits it:** two teams' extension methods both registering
  IEmailSender; or a test override that stops overriding after an innocent
  reorder of builder calls.
- **Repro:** register two implementations of one interface; resolve - only
  the last runs; swap the two registration lines - behavior flips with a diff
  that looks like formatting. `#:package Microsoft.Extensions.DependencyInjection@10.*`,
  `#:property PublishAot=false`. Deterministic.
- **Damage:** production behavior decided by call order in composition code,
  ungreppable from any use site; debugging happens in an implementation that
  is never actually invoked.
- **😈 seed:** `TryAdd*` exists precisely for this and *inverts* the rule -
  first wins. Two idioms, opposite winners, both silent.
- **Verified:** documented container behavior; verify at build.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **scoped-from-root-lives-forever** (A6,5) - resolving a scoped service
  straight from the root IServiceProvider gives it singleton lifetime by
  accident: never disposed, per-request state leaking across requests.
  Mirror image of shipped #0022 (the-captive-scoped) - check its README/😈
  overlap before promoting.
