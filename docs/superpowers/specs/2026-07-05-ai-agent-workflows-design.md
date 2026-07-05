# AI Agent Workflows (CI-side) — Design Spec

**Date:** 2026-07-05
**Branch:** feature/aidlc-enhancements
**Status:** Approved (brainstormed section-by-section)

## Purpose

Two GitHub Actions workflows that make the repo's AI agent operate on GitHub itself, using Claude Code CLI in headless `--print` mode (Approach 1 of 3 considered; GitHub-MCP and JSON-handoff variants rejected for CI debuggability):

1. **AI PR Review** — every non-draft PR gets one sticky comment containing a summary, impact analysis, and structured code review, plus an `ai-reviewed` label.
2. **AI Issue Implementation** — labeling an issue `ai-implement` makes the agent analyze it, implement on a branch, and open a **draft** PR. The agent never merges.

Deployment environments and `deployed:*` labels are out of scope (separate future spec).

## Component 1 — `.github/workflows/ai-pr-review.yml`

- **Triggers:** `pull_request` (opened, synchronize, reopened); `workflow_dispatch` with `pr_number` for on-demand testing.
- **Skips:** draft PRs; bot/`ai/*`-authored PRs (prevents recursion; opt-in via `ai-review-requested` label); `ai-skip` label.
- **Permissions:** `contents: read`, `pull-requests: write`. **Timeout:** 15 min. **Concurrency:** per-PR group, cancel-in-progress.
- **Steps:** checkout (fetch-depth 0) → install Claude Code CLI → scoped diff (`*.cs *.csproj *.yml *.yaml`, truncate at 800 lines with note) → build prompt from `docs/prompts/pr-review-agent.md` (inject CLAUDE.md + PR metadata + diff) → `claude --print` (no file/shell tools) → sticky comment (`gh pr comment --edit-last`) → add `ai-reviewed` label (created idempotently) → verdict `🚫 Do not merge` also adds `ai-blocked`.
- **Comment structure (prompt-enforced):** `### Summary` (2-3 sentences) / `### Impact` (Clean Architecture layers touched) / `### Code Review` (findings, file:line, severity) / `### Verdict` (✅ / ⚠️ / 🚫 + one line).

## Component 2 — `.github/workflows/ai-issue-implement.yml`

- **Triggers:** `issues: labeled` gated on `ai-implement`; `workflow_dispatch` with `issue_number`.
- **Permissions:** `contents: write`, `pull-requests: write`, `issues: write`. **Timeout:** 30 min.
- **Steps:** checkout + CLI install → `gh issue view` → **analysis pass** (`claude --print`, `docs/prompts/issue-implement-agent.md`: plan only, no code) → branch `ai/issue-$N-$SLUG` → **implementation pass** (headless, `Edit,Write,Read` allowed, `Bash` disallowed) → no changes? comment plan + `ai-needs-human` label, exit 0 → commit `ai: implement #N — title`, push → `gh pr create --draft` (body: `Closes #N`, AI disclaimer, plan) → comment link on issue + `ai-drafted` label.
- Draft PRs from this workflow do not trigger the review workflow (draft + bot skip); marking ready-for-review fires normal CI.

## Prompts

`docs/prompts/pr-review-agent.md` and `docs/prompts/issue-implement-agent.md` — same conventions as the existing prompt library (placeholders `{CLAUDE_MD}`, `{DIFF}`, `{ISSUE_TITLE}`…, when-to-use section, human-usable as-is).

## Labels

`ai-implement` (human→issue), `ai-drafted`, `ai-needs-human` (agent→issue), `ai-reviewed`, `ai-blocked` (agent→PR), `ai-skip`, `ai-review-requested` (human→PR). Created idempotently on first run.

## Error handling & cost

Missing `ANTHROPIC_API_KEY` or failed Claude call → short "AI review unavailable" comment / `ai-needs-human`, **exit 0** — AI gates never fail CI on their own infrastructure. Cost bounded by diff truncation, concurrency cancellation, skip conditions, timeouts; no scheduled runs.

## Testing

`workflow_dispatch` first against an existing PR and a trivial issue (e.g. "add unit test for UriComposer") before event triggers are relied on.
