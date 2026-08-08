# 🪆 inheritance

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### default-arg-from-static-type (A4)

- **Twist:** the override changed the default to `urgent = true` - and
  every caller through the base reference still runs the derived body with
  the base's `false`: defaults are compile-time property of the declared
  type, the body is runtime property of the object, and they mix.
- **Mechanic:** default arguments are baked into each call site by the
  compiler from the static type's declaration; virtual dispatch then picks
  the body from the runtime type - so a base-typed call executes derived
  code with base constants. The interface variant is sharper: interface
  default 3, class default 1 - the same object runs with 3 through the
  interface and 1 through the class, and DI hands everyone the interface.
  No warning exists for a default mismatch.
- **Who hits it:** overrides and interface implementations that "fix" a
  default - retries, timeouts, urgency flags. Only direct users of the
  concrete class see the fix; framework and DI paths, which call through
  the abstraction, keep the old number.
- **Repro:** virtual `Send(string msg, bool urgent = false)` overridden
  with `urgent = true`: calls through Notifier and PagerNotifier print
  False then True from one object. Interface `Run(int attempts = 3)`
  implemented with `attempts = 1`: 3 via the interface, 1 via the class.
  Deterministic, no packages, zero warnings.
- **Damage:** production behavior splits by call path - alerts stay
  non-urgent and retries stay at the old count precisely on the paths that
  matter (the abstracted ones), and no diff explains it because the
  change *is* in the code, just not in the constant the caller baked.
- **😈 seed:** the baked constant lives in the *caller's* binary: change a
  library's default and every already-compiled consumer keeps the old
  value until it recompiles - one default, upgraded non-atomically across
  the ecosystem.
- **Verified:** ran on .NET 10 (2026-07-22): base ref ran the derived body
  with urgent=False, derived ref with True; interface path got 3, class
  path 1; no compiler warnings.

### the-overload-that-stole-the-call (A4,5)

- **Twist:** base declares `Save(int)`, derived adds `Save(long)` - and
  `repo.Save(5)` calls the long one: overload resolution stops at the
  first type with *any* applicable method, so a worse match nearby beats
  an exact match one level up.
- **Mechanic:** member lookup walks from the most-derived type toward the
  base and, at the first level where any candidate is applicable, stops -
  base declarations never enter the candidate set. Closer beats better: an
  exact-match `int` in base loses to a conversion-needing `long` (or
  `object`, or a generic `M<T>`) in derived. Casting the receiver to the
  base type restores the exact match - same argument, two methods, chosen
  by the reference.
- **Who hits it:** the "add-only" PR: a convenience overload added to a
  derived class silently reroutes existing calls that never mentioned it -
  no call site changed, no override involved, no warning emitted. Wrapper
  and decorator hierarchies are the natural habitat.
- **Repro:** `Repo.Save(int)` / `AuditedRepo.Save(long)`:
  `new AuditedRepo().Save(5)` prints the long version, `((Repo)r).Save(5)`
  the int one. Second act: `Log(string)` in base vs `Log(object)` in
  derived - the vaguer object overload wins over the exact string match.
  Deterministic, no packages.
- **Damage:** behavior rerouted by an additive change: the converted /
  differently-audited path runs where the precise one used to, every
  existing call site recompiles onto the thief, and the diff that did it
  only *added* a method.
- **😈 seed:** the generic flavor steals universally: adding `Handle<T>(T)`
  to a derived type captures *every* call that base's `Handle(int)` used
  to serve - type inference makes the thief applicable to anything
  (verified: T inferred as Int32, exact base match ignored).
- **Verified:** ran on .NET 10 (2026-07-22): Save(5) resolved to derived
  Save(long), cast to base restored Save(int); Log("hello") picked derived
  Log(object) over base's exact Log(string); Handle(5) picked derived
  Handle&lt;T&gt; over base's Handle(int).
