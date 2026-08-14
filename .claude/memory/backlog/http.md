# 🌐 http

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### timeout-looks-like-cancellation (A4,5)

- **Twist:** the server timing out and the user pressing Cancel throw the
  *same exception type* - so `catch (OperationCanceledException)` swallows
  the timeout as "user changed their mind" and skips exactly the retry a
  timeout deserves.
- **Mechanic:** HttpClient.Timeout fires an internal cancellation, and
  the result is TaskCanceledException - the same type a caller's token
  produces. Since .NET 5 the payloads differ: the timeout carries
  InnerException = TimeoutException, the user cancel carries the
  caller's token (e.CancellationToken == cts.Token). The type system
  distinguishes nothing; only inspecting the payload does.
- **Who hits it:** resilience code. Retry-on-timeout / don't-retry-on-
  cancel is the standard policy, and the natural catch-by-type shape
  gets one direction wrong - timeouts silently never retried, or user
  cancels pointlessly retried.
- **Repro:** stub handler awaiting Task.Delay(Infinite, ct):
  client.Timeout = 200 ms gives TaskCanceledException with inner
  TimeoutException; a user CTS gives the same type with the matching
  token; one conflating catch handles both identically. The
  never-completing handler makes the timeout certain - deterministic, no
  races, no network, no packages.
- **Damage:** transient timeouts treated as intentional aborts: no retry,
  no alert, the request quietly dropped - the resilience layer everyone
  trusts is the component eating the failures.
- **😈 seed:** the metrics inherit the confusion: cancels are "user
  behavior", timeouts are "incidents", and one catch block funnels both
  into whichever counter it was written for - the outage graph stays
  flat while users mash Cancel.
- **Verified:** ran on .NET 10 (2026-07-24): timeout gave inner
  TimeoutException; cancel gave the same outer type with the caller's
  token; both landed in a single OperationCanceledException catch.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **http:** socket exhaustion and stale-DNS lore still fail the
  single-file determinism bar - but the stub-HttpMessageHandler
  technique (proven by the three entries above) covers everything that
  happens above the socket: prefer it for future candidates.

- **leading-slash-ignores-baseaddress** (A4) - a request path starting with
  "/" throws away the BaseAddress path entirely: with base `.../v1`,
  `GetAsync("/users")` goes to `/users`. Pure Uri math - same BaseAddress-path
  family as the declined `baseaddress-eats-your-path` (see rejected.md);
  reconsider only if a distinctly different angle emerges.

- **the-client-frozen-by-first-use** - after the first request, setting
  HttpClient.Timeout or BaseAddress throws InvalidOperationException
  ("This instance has already started one or more requests" - verified
  2026-07-24). Shared/static clients are the recommended pattern, so any
  later per-operation tweak is a crash; needs a sharper who-hits-it
  before promoting.
