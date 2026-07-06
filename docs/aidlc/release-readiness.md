# Release-readiness note — JWT signing key from configuration

| Area | Status |
|---|---|
| Change summary | Removed hardcoded JWT signing key; key now resolves from `Tokens:Key` config or `JWT_SECRET_KEY` env var via `JwtTokenKeyResolver`. Missing key in non-Development fails fast at startup with an actionable error. Dev-only fallback keeps the demo runnable. |
| Build status | 0 Errors, 192 Warnings (pre-existing warnings unrelated to this change) |
| Test status | UnitTests 50/50, IntegrationTests 3/3, FunctionalTests 12/12, PublicApiIntegrationTests 54/54 |
| Security/quality checks | Gitleaks: hardcoded key removed from source (allowlist covers legacy string in git history). NuGetAudit: no new advisories. Dependency review: `Microsoft.Extensions.Hosting.Abstractions` 10.0.0 added to Infrastructure (no audit findings). |
| Observability impact | Startup failure is loud and actionable (InvalidOperationException naming both config keys). No key material logged anywhere. |
| Rollback consideration | `git revert` of this squash commit restores previous behavior; no data or schema impact. Deployed environments must set `Tokens__Key` or `JWT_SECRET_KEY` (≥32 bytes) BEFORE deploying this change. |
| Human review required | Confirm production/staging deployment config defines `Tokens__Key` or `JWT_SECRET_KEY` (≥32 bytes, e.g. `openssl rand -base64 48`) before rollout. |
