# AIDLC Persona Pipeline — Design Spec

**Date:** 2026-07-05
**Branch:** feature/aidlc-enhancements
**Status:** Approved for planning (auto mode)

## Purpose

A gated, persona-aware AI development pipeline for eShopOnWeb, run locally through Claude Code. One entry point takes a ticket from requirements interrogation to a human-approved PR, with test generation up front, separated doer/reviewer contexts, cost telemetry at every stage, and local testing gated behind all review steps.

Built repo-local first (`.claude/skills/`, `config/`, `docs/artifacts/`) and structured for later extraction into a distributable plugin.

Complements — does not replace — the CI-side agents designed in `2026-07-05-ai-agent-workflows-design.md` (AI PR review + issue→draft-PR GitHub Actions).

## Pipeline

```
/aidlc --ticket=<N>
[0] Classify (haiku)  → persona + phase_set → progress.json created
[1] Requirements      → human interrogation, ≤10 questions → requirements.md + ACs
[2] QA Generate       → manual TCs + automation skeletons (xUnit / Playwright) from ACs
[3] Design            → design.md (skipped for Bug-fix / A11y personas)
[4] Implementation    → DOER context writes code           ◄──────────────┐
[5] Build + Unit      → dotnet build + dotnet test (unit only)            │
[6] Code Review       → FRESH subagent context, findings artifact         │ loopback
[7] Security Scan     → FRESH subagent context, OWASP + secrets           │ on any
[8] Local Testing     → run generated automation + Playwright UI tests    │ failure
                        ONLY reached when 5–7 are clean ──────────────────┘
[9] Readiness Report  → confidence score, AC coverage matrix, risk register
[10] HUMAN GATE       → approve → PR generation (haiku) + cost summary (+ADO)
```

Each phase boundary: validate artifact → gate. A failed validation suppresses the approve option, so the pipeline cannot advance on a broken artifact. `--resume` restarts from the last checkpoint recorded in progress.json.

## Personas (Phase 0 routing)

| Persona | Phase set | Notes |
|---|---|---|
| BA | 0–2 only | Deliverable is requirements.md + ACs + test cases; no code |
| Backend | all | QA-generate emits xUnit + integration test skeletons |
| UI | all | QA-generate emits Playwright specs; local testing runs headed browser tests |
| Bug-fix | 0,1(short),2,4–10 | Skips design; requirements phase capped at 5 questions |
| A11y bug-fix | 0,1(short),2,4–10 | Playwright + axe-core checks in phases 2 and 8 |

Mature state (explicit goal): this is already one pipeline with conditional skill invocation — adding a persona = adding one skill file + one row in `config/personas.json`.

## Key rules

1. **Requirements interrogation (Phase 1):** one question at a time via AskUserQuestion, hard cap 10 (5 for bug-fix personas). Output `requirements.md`: problem statement, in/out of scope, numbered acceptance criteria (AC-1…AC-n), open assumptions. ACs are the contract every later phase traces back to.
2. **Tests before code (Phase 2):** functional test cases (Given/When/Then per AC) plus runnable automation skeletons generated before implementation. Implementation's definition of done includes making these pass.
3. **Progress tracking:** `docs/artifacts/<ticket>/progress.json` — schema: ticket, persona, phase_set, per-phase {status, started, finished, model, input_tokens, output_tokens, cost_usd, loopbacks}, checkpoint. `PROGRESS.md` is the human-readable rendering, updated at every phase boundary (haiku).
4. **Cost telemetry:** every phase appends token/cost rows. Capture mechanism: subagent/headless phases parse the usage block from `claude -p --output-format json`; in-session phases use a Stop-hook token tracker reading the session transcript. Pricing table in `config/pipeline-models.json`. End of flow: `cost-summary.json` {ticket, persona, total_tokens, total_cost_usd, per_phase[], loopback_cost_usd}. If `AZURE_DEVOPS_PAT` + org/project config present in `.claude/sdlc/ado.json`, post the summary as a work-item comment via `az boards`; otherwise the JSON file is the contract for the sprint-level ADO utility. Never fail the pipeline on ADO write failure.
5. **Context separation:** Phases 6, 7, and 8's triage run as fresh subagent contexts (Agent tool) receiving only the artifacts (diff, requirements.md, ACs) — never the doer's conversation. The doer never reviews its own work.
6. **Local testing last + loopback:** Phase 8 runs only when build, unit tests, code review, and security scan are all clean. Failures at 6/7/8 produce a findings artifact and return to Phase 4 with findings as input; `loopbacks` counter increments; 3 loopbacks on one phase → escalate to human.
7. **Model routing** (`config/pipeline-models.json`): haiku — classification, progress/PROGRESS.md updates, PR title/body generation, ADO updates, cost summarization; session default (sonnet/opus tier) — requirements, design, implementation, review, security, readiness.
8. **Readiness before PR (Phase 9):** `readiness.md` — confidence score 0–100 with one-line justification, AC coverage matrix (AC → implementing files → tests → pass/fail evidence), risk register (likelihood × impact, mitigations), ship/no-ship recommendation. Human sees this at the Phase 10 gate; only after approval does PR generation run.

## Repo layout added

```
.claude/skills/aidlc-pipeline/SKILL.md      orchestrator (/aidlc)
.claude/skills/aidlc-requirements/…         Phase 1
.claude/skills/aidlc-qa-generate/…          Phase 2
.claude/skills/aidlc-design/…               Phase 3
.claude/skills/aidlc-review/…               Phase 6 (subagent prompt)
.claude/skills/aidlc-security/…             Phase 7 (subagent prompt)
.claude/skills/aidlc-local-test/…           Phase 8
.claude/skills/aidlc-readiness/…            Phase 9
.claude/skills/aidlc-pr/…                   Phase 10 (haiku)
config/personas.json                        persona → phase_set
config/pipeline-models.json                 phase → model + pricing
docs/artifacts/<ticket>/                    requirements.md, testcases.md, design.md,
                                            findings-*.md, readiness.md,
                                            progress.json, PROGRESS.md, cost-summary.json
.claude/sdlc/ado.json (optional, gitignored) ADO org/project/work-item config
```

Safety hooks (added to `.claude/settings.json`): secrets/PII prompt guard, destructive-command guard, .env read/write guard.

## Error handling

- Phase validator fails → gate blocks, artifact must be fixed; no silent advance.
- Subagent (review/security) dies → retry once, then mark phase `error` in progress.json and stop at gate.
- Playwright/local tests can't run (app won't start) → treated as Phase 8 failure with findings, not skipped.
- ADO write failure → warn, continue; cost-summary.json always written.

## Testing the pipeline itself

Dry-run with a seeded sample ticket (`docs/artifacts/SAMPLE-1/ticket.md`, a small UriComposer bug) through Bug-fix persona end-to-end; assert progress.json schema, artifact presence per phase, loopback behavior (inject a deliberate review finding), and cost-summary totals = sum of phases.

## Build order (implementation waves)

1. **Wave 1 — skeleton:** progress.json/PROGRESS.md tracking, orchestrator skill, classifier, personas.json, Bug-fix + Backend personas, phases 1/4/5/10.
2. **Wave 2 — quality gates:** QA-generate, review + security subagents, loopback logic, readiness report.
3. **Wave 3 — UI + local testing:** Playwright integration, UI + A11y personas, Phase 8 ordering rule.
4. **Wave 4 — telemetry:** model routing, cost tracking, cost-summary.json, optional ADO write, safety hooks.

Out of scope: deployment environments/labels (separate spec), multi-repo distribution (extract-to-plugin comes after the pipeline proves itself here).
