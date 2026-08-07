# 🌐 http

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### no-ensuresuccess-reads-error-body (A4,5)

- **Twist:** GetAsync happily returns the 500, the ProblemDetails error
  body deserializes into your DTO as an all-defaults object, and the
  pipeline continues with a zero-total order - while the adjacent
  spelling, GetFromJsonAsync, would have thrown.
- **Mechanic:** HttpClient throws on non-success only in some spellings:
  SendAsync/GetAsync return the response whatever the status; the
  convenience readers (GetStringAsync, GetFromJsonAsync) call
  EnsureSuccessStatusCode internally. So the "more explicit" pattern -
  GetAsync + read + deserialize - is exactly the one with no guard. And
  modern APIs return JSON error bodies (ProblemDetails), which bind into
  a typical DTO as defaults with zero errors: Id=null, Total=0.
- **Who hits it:** every hand-rolled API client that fetches with
  GetAsync "because we need the status/headers" and deserializes the
  content - minus the one EnsureSuccessStatusCode line the tutorial
  showed and the copy-paste dropped.
- **Repro:** stub HttpMessageHandler returning 500 with a ProblemDetails
  JSON body: GetAsync no throw; deserialize into Order gives all
  defaults; GetFromJsonAsync on the same route throws
  HttpRequestException with StatusCode. All in-process, no network, no
  packages (System.Net.Http.Json is in-box);
  `#:property PublishAot=false`.
- **Damage:** the outage converts itself into corrupt records: zero-priced
  orders and null ids flow downstream precisely while the upstream is
  failing - the moment logs matter most, the data lies hardest.
- **😈 seed:** half the codebase is immune by accident - the corners that
  used GetFromJsonAsync throw properly - so the bug reads as
  "intermittent" across services, and review cannot spot the difference
  without knowing which method family each client used.
- **Verified:** ran on .NET 10 (2026-07-24): 500 returned without
  exception, ProblemDetails bound into Order as Id=null/Total=0,
  GetFromJsonAsync threw with StatusCode=InternalServerError.

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

### request-messages-are-single-use (A5)

- **Twist:** the retry helper catches the 503, loops, resends - and
  throws InvalidOperationException: an HttpRequestMessage can be sent
  exactly once, so the code path built to survive failures is the only
  path that crashes.
- **Mechanic:** SendAsync marks the message sent and rejects reuse ("The
  request message was already sent. Cannot send the same request message
  multiple times."). The rule exists because request content streams may
  be consumed - but it applies to every message, contentless GETs
  included. Retry code that hoists the message out of the loop as
  loop-invariant setup works on the happy path and detonates on the
  first real retry. The fix is one line: build (or clone) the message
  per attempt.
- **Who hits it:** hand-rolled retry loops and resilience wrappers around
  SendAsync, and 401-refresh middleware that replays the original
  request after re-authenticating.
- **Repro:** stub handler answering 503 then 200: first send 503; second
  send of the *same* message throws InvalidOperationException; a fresh
  message gets the 200. In-process, deterministic, no packages.
- **Damage:** the safety net is the crash site - transient failures that
  one resend would absorb surface instead as an exception from inside
  the retry helper, upgrading a blip into an error page.
- **😈 seed:** it hides by construction: the reuse only executes when a
  retry executes, so 100% of happy-path traffic - and every test whose
  stub returns success first - certifies the broken loop.
- **Verified:** ran on .NET 10 (2026-07-24): 503, then
  InvalidOperationException on resending the same message, then 200 with
  a fresh one.

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
