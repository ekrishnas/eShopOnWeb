# AI-Driven Development Lifecycle (AIDLC) in this repository

How AI assistance and automated gates combine at every lifecycle stage.
Principle: **AI accelerates, automation gates, humans decide.**

| Stage | AI practice | Automated gate |
|---|---|---|
| Understand | Claude Code explores with `CLAUDE.md` as onboarding context | — |
| Design | Brainstorm approaches/trade-offs before code; spec captured in the issue/PR | PR template requires "What & why" |
| Code | Claude Code implements small, reviewed increments per CLAUDE.md guardrails | Build + `dotnet format` gates |
| Test | `.claude/commands/gen-tests.md` generates xUnit v3 tests; humans audit assertions | Test run + coverage threshold gate |
| Review | `.claude/commands/code-review.md` checklist; optional AI PR reviewer workflow | Human approval required to merge |
| Security | `.claude/commands/security-check.md` on every diff | CodeQL (SAST), Gitleaks (secrets), dependency review + NuGetAudit (SCA) |
| Release | `.claude/commands/release-note.md` produces the readiness note | All gates green = releasable; note reviewed by a human |
| Operate | Weekly package-watch issue triaged; Dependabot PRs reviewed with AI risk summary (roadmap) | `package-watch.yml` scheduled report |

## Gates (all open source / free)

1. **Build/Test/Coverage** — `.github/workflows/dotnetcore.yml`: restore→build→test with coverage threshold (fails below baseline).
2. **Lint** — `dotnet format --verify-no-changes` (`.github/workflows/format-check.yml`).
3. **SAST** — `.github/workflows/codeql.yml` (C# + JS, PR + weekly).
4. **Secrets** — `.github/workflows/secrets-scan.yml` (Gitleaks, tuned by `.gitleaks.toml`).
5. **SCA** — `dependency-review-action` on PRs; NuGetAudit (NU1901–NU1904 as errors in CI); Dependabot (nuget + github-actions).
6. **Package hygiene** — `.github/workflows/package-watch.yml` weekly stale/vulnerable report → tracking issue.

## Agentic flows

- **Issue → PR** (`claude-issue-to-pr.yml`): mention `@claude` in an issue
  (or label `ai-fix`); Claude Code implements per CLAUDE.md, runs tests,
  opens a PR that must pass every gate above. Requires `ANTHROPIC_API_KEY`
  or `CLAUDE_CODE_OAUTH_TOKEN` repo secret; inert without it.
- **AI PR review** (`claude-pr-review.yml`): on PR open, Claude posts a
  review using the code-review checklist. Advisory only — humans merge.

## Roadmap (designed, not yet enabled)

1. **CI-failure auto-triage** — on red workflow, Claude comments root cause analysis on the PR.
2. **Dependabot PR triage** — Claude summarizes changelog/breaking-change risk on each dependency bump.
3. **AI release notes** — on tag, draft notes from merged PRs.
4. **Docs-drift check** — advisory comment when a diff makes CLAUDE.md/docs stale.

## Metrics to watch (adoption honesty)

Lead time per PR, escaped-defect count, coverage trend, gate-failure rate by gate, % PRs with AI assistance declared.
