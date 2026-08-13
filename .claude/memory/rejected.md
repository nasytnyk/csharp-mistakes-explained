# Rejected

Candidates the curator declined - **do not rebuild or re-propose them**. His
reasons were case-by-case and mostly a gut call, so only the list matters, not the
why. (Shipped exhibits and numbering live in `state.md`.)

## Declined candidates

- turkish-i-login
- int-overflow-in-cart
- path-combine-betrayal
- .Result deadlock — the SynchronizationContext form only; the pool-starvation form shipped as #0035
- StringBuilder-in-a-loop
- quadratic ElementAt
- culture/timezone bug without pinning
- datetime-kind-round-trip
- enum-accepts-undefined
- lock-on-a-string
- sort-is-unstable
- firstordefault-on-structs
- semaphore-never-released
- the-cached-failure
- threadlocal-doesnt-follow
- the-uncancellable-stream
- the-timeout-that-stopped-nothing
- the-self-deadlock
- the-hijacked-completion
- the-eager-throw
- the-linked-leak
- asynclocal-never-flows-up
- trywrite-drops-silently
- use-after-return
- the-oversized-rental
- the-cache-that-owns-its-keys
- large-array-born-in-gen2
- finalizer-delays-gc
- mutating-a-boxed-struct
- ternary-unifies-then-boxes
- boxed-enum-isnt-its-number
- static-field-per-closed-type
- t-question-mark-is-not-nullable
- the-oblivious-boundary
- the-stale-narrowing
- guessable-random
- interpolated-injection
- assert-equal-floats-no-tolerance
- async-void-test-always-passes
- static-state-leaks-between-tests
- length-lies-about-emoji
- baseaddress-eats-your-path
- hasflag-zero-always-true
- the-25-hour-day
- poisoned-static-constructor
- oftype-eats-the-evidence
- the-renumbered-status
- the-overlapping-flags
- enum-default-is-zero
- isdefined-rejects-legal-flags
- stale-tracked-entity

## Retired topics — do not propose the area at all

- **regex** — entire hall retired (its `halls.md` row and `backlog/regex.md` were deleted).
- **Timer / `System.Threading.Timer`** — topic out; both candidates declined (the-overlapping-timer, the-collected-timer).
