# 🌐 http

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

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
