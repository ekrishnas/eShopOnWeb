# Tech-debt register

Deliberately deferred items, discovered during the 2026-07 AIDLC review.
Each entry: impact, suggested fix, and why it was deferred.

| # | Item | Impact | Suggested fix | Why deferred |
|---|---|---|---|---|
| 1 | Basket/order flow error handling — `BasketService.TransferBasketAsync` and order creation lack defensive handling for concurrent modification and missing-item cases | Medium: 500s under race conditions | Add guard clauses + domain exceptions + tests | Kept the assessment change controlled (one improvement) |
| 2 | Test coverage gaps — several ApplicationCore services and PublicApi endpoints lack unit tests | Medium: regressions ship silently | Use `.claude/commands/gen-tests.md` per service; raise coverage gate as it climbs | Coverage gate now prevents further decline; raising takes time |
| 3 | `AuthorizationConstants.AUTH_KEY` appears unused | Low: dead code confuses readers | Confirm no reflection/config usage, then delete | Out of scope of JWT change |
| 4 | Historical hardcoded JWT key remains in git history | Low: old key must never be reused | Key is invalidated by config-based resolution; rotating history (filter-repo) not worth the disruption on a fork | Accepted risk, documented |
| 5 | `richnav.yml` / `toc.yml` workflows unreviewed | Low | Audit or remove upstream-specific workflows | Not gate-relevant |
