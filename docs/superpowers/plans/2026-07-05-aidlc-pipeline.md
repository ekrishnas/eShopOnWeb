# AIDLC Persona Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Implementation subagents must use model `sonnet` (user directive).**

**Goal:** A gated, persona-aware `/aidlc --ticket=<ID>` pipeline in this repo: requirements interrogation → upfront test generation → implementation → fresh-context review/security → local testing (last, with loopback) → readiness report → human gate → PR, with per-phase cost telemetry.

**Architecture:** Claude Code project skills under `.claude/skills/aidlc-*` orchestrated by `aidlc-pipeline`. All state lives in git-tracked artifacts under `docs/artifacts/<ticket>/` (progress.json is the single source of truth). Reviewer/security/QA phases run as fresh subagent contexts receiving only artifacts. Config in `config/*.json`.

**Tech Stack:** Claude Code skills (markdown), JSON config, PowerShell for hooks/validation (Windows dev machines), xUnit/Playwright for generated tests.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-05-aidlc-pipeline-design.md` — follow exactly.
- Phase order is fixed: local testing (8) runs only after build+unit (5), review (6), security (7) are clean. Loopback returns to phase 4; 3 loopbacks on one phase → escalate to human.
- Requirements questions: hard cap 10 (5 for BugFix/A11yBugFix personas), one at a time via AskUserQuestion.
- Doer never reviews its own work: phases 6, 7 run via Agent tool subagents given only artifacts.
- Model routing: haiku for phases 0, 5, 8, 10 and progress updates; sonnet for phases 1, 2, 3, 4, 6, 7, 9.
- Every phase boundary: update `progress.json` + `PROGRESS.md`, then gate with AskUserQuestion (Approve suppressed if artifact validation failed).
- ADO write is optional and never fails the pipeline.
- Commit after every task on `feature/aidlc-enhancements`.

## File Structure

```
config/personas.json                          persona → phase_set + options
config/pipeline-models.json                   model ids + pricing + phase routing
.claude/commands/aidlc.md                     /aidlc entry point (thin wrapper)
.claude/skills/aidlc-pipeline/SKILL.md        orchestrator
.claude/skills/aidlc-pipeline/references/progress-template.json
.claude/skills/aidlc-pipeline/references/gate-rules.md
.claude/skills/aidlc-requirements/SKILL.md    phase 1
.claude/skills/aidlc-qa-generate/SKILL.md     phase 2
.claude/skills/aidlc-design/SKILL.md          phase 3
.claude/skills/aidlc-review/SKILL.md          phase 6 (subagent brief)
.claude/skills/aidlc-security/SKILL.md        phase 7 (subagent brief)
.claude/skills/aidlc-local-test/SKILL.md      phase 8
.claude/skills/aidlc-readiness/SKILL.md       phase 9
.claude/skills/aidlc-pr/SKILL.md              phase 10
scripts/hooks/guard-secrets.ps1               UserPromptSubmit hook
scripts/hooks/guard-destructive.ps1           PreToolUse(Bash) hook
scripts/hooks/guard-env.ps1                   PreToolUse(Read|Edit|Write) hook
docs/artifacts/README.md                      artifact dir contract
docs/artifacts/SAMPLE-1/ticket.md             dry-run ticket
```

---

## Wave 1 — Skeleton

### Task 1: Config files

**Files:**
- Create: `config/personas.json`
- Create: `config/pipeline-models.json`

**Interfaces:**
- Produces: `personas.json` keys `BA|Backend|UI|BugFix|A11yBugFix`, each `{phases:int[], maxQuestions:int, localTest:"none"|"dotnet"|"playwright"|"playwright-axe"}`. `pipeline-models.json` keys `models` (id + USD per MTok) and `phase_models` (phase number → model key). All later tasks read these.

- [ ] **Step 1: Write `config/personas.json`:**

```json
{
  "BA":         { "phases": [0, 1, 2],                        "maxQuestions": 10, "localTest": "none" },
  "Backend":    { "phases": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10], "maxQuestions": 10, "localTest": "dotnet" },
  "UI":         { "phases": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10], "maxQuestions": 10, "localTest": "playwright" },
  "BugFix":     { "phases": [0, 1, 2, 4, 5, 6, 7, 8, 9, 10],    "maxQuestions": 5,  "localTest": "dotnet" },
  "A11yBugFix": { "phases": [0, 1, 2, 4, 5, 6, 7, 8, 9, 10],    "maxQuestions": 5,  "localTest": "playwright-axe" }
}
```

- [ ] **Step 2: Write `config/pipeline-models.json`:**

```json
{
  "models": {
    "haiku":  { "id": "claude-haiku-4-5-20251001", "input_per_mtok": 1.00, "output_per_mtok": 5.00 },
    "sonnet": { "id": "claude-sonnet-4-6",         "input_per_mtok": 3.00, "output_per_mtok": 15.00 }
  },
  "phase_models": {
    "0": "haiku", "1": "sonnet", "2": "sonnet", "3": "sonnet", "4": "sonnet",
    "5": "haiku", "6": "sonnet", "7": "sonnet", "8": "haiku", "9": "sonnet", "10": "haiku"
  },
  "mechanical_tasks_model": "haiku"
}
```

- [ ] **Step 3: Verify both parse**

Run (PowerShell): `Get-Content config/personas.json | ConvertFrom-Json; Get-Content config/pipeline-models.json | ConvertFrom-Json`
Expected: objects printed, no error.

- [ ] **Step 4: Commit**

```bash
git add config/
git commit -m "feat(aidlc): add persona and model-routing config"
```

---

### Task 2: Progress tracking contract

**Files:**
- Create: `.claude/skills/aidlc-pipeline/references/progress-template.json`
- Create: `docs/artifacts/README.md`

**Interfaces:**
- Produces: progress.json schema every skill writes to. Phase entry shape: `{"phase":int,"name":string,"status":"pending|in_progress|done|error|skipped","started":iso8601|null,"finished":iso8601|null,"model":string,"input_tokens":int,"output_tokens":int,"cost_usd":number,"loopbacks":int,"artifact":string|null}`.

- [ ] **Step 1: Write `progress-template.json`:**

```json
{
  "ticket": "",
  "title": "",
  "persona": "",
  "phase_set": [],
  "current_phase": 0,
  "checkpoint": null,
  "created": "",
  "updated": "",
  "phases": [
    { "phase": 0,  "name": "Classify",       "status": "pending", "started": null, "finished": null, "model": "haiku",  "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": null },
    { "phase": 1,  "name": "Requirements",   "status": "pending", "started": null, "finished": null, "model": "sonnet", "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "requirements.md" },
    { "phase": 2,  "name": "QA Generate",    "status": "pending", "started": null, "finished": null, "model": "sonnet", "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "testcases.md" },
    { "phase": 3,  "name": "Design",         "status": "pending", "started": null, "finished": null, "model": "sonnet", "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "design.md" },
    { "phase": 4,  "name": "Implementation", "status": "pending", "started": null, "finished": null, "model": "sonnet", "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": null },
    { "phase": 5,  "name": "Build+Unit",     "status": "pending", "started": null, "finished": null, "model": "haiku",  "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": null },
    { "phase": 6,  "name": "Code Review",    "status": "pending", "started": null, "finished": null, "model": "sonnet", "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "findings-review.md" },
    { "phase": 7,  "name": "Security Scan",  "status": "pending", "started": null, "finished": null, "model": "sonnet", "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "findings-security.md" },
    { "phase": 8,  "name": "Local Testing",  "status": "pending", "started": null, "finished": null, "model": "haiku",  "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "findings-localtest.md" },
    { "phase": 9,  "name": "Readiness",      "status": "pending", "started": null, "finished": null, "model": "sonnet", "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "readiness.md" },
    { "phase": 10, "name": "PR",             "status": "pending", "started": null, "finished": null, "model": "haiku",  "input_tokens": 0, "output_tokens": 0, "cost_usd": 0, "loopbacks": 0, "artifact": "cost-summary.json" }
  ]
}
```

- [ ] **Step 2: Write `docs/artifacts/README.md`:**

```markdown
# Pipeline Artifacts

One directory per ticket: `docs/artifacts/<TICKET-ID>/`.

| File | Written by | Purpose |
|---|---|---|
| ticket.md | human | Ticket description (input) |
| progress.json | orchestrator | Single source of truth: persona, phase statuses, tokens, cost, loopbacks, checkpoint |
| PROGRESS.md | orchestrator | Human-readable rendering of progress.json |
| requirements.md | phase 1 | Problem, scope, numbered ACs (AC-1…), assumptions |
| testcases.md | phase 2 | Given/When/Then per AC + paths of generated automation skeletons |
| design.md | phase 3 | Design notes (skipped for bug-fix personas) |
| findings-review.md | phase 6 | Code-review findings (fresh context) |
| findings-security.md | phase 7 | Security findings (fresh context) |
| findings-localtest.md | phase 8 | Local/UI test failures |
| readiness.md | phase 9 | Confidence score, AC coverage matrix, risk register |
| cost-summary.json | phase 10 | Totals for sprint-level cost rollup (ADO utility contract) |

`progress.json` phase entry: `{phase, name, status: pending|in_progress|done|error|skipped, started, finished, model, input_tokens, output_tokens, cost_usd, loopbacks, artifact}`.

`cost-summary.json`: `{ticket, persona, total_input_tokens, total_output_tokens, total_cost_usd, loopback_cost_usd, per_phase: [{phase, name, cost_usd}]}`.
```

- [ ] **Step 3: Verify** — `Get-Content .claude/skills/aidlc-pipeline/references/progress-template.json | ConvertFrom-Json` parses; template has 11 phase entries.

- [ ] **Step 4: Commit**

```bash
git add .claude/skills/aidlc-pipeline/references/progress-template.json docs/artifacts/README.md
git commit -m "feat(aidlc): progress-tracking contract and artifacts dir"
```

---

### Task 3: Orchestrator skill + /aidlc command + gate rules

**Files:**
- Create: `.claude/skills/aidlc-pipeline/SKILL.md`
- Create: `.claude/skills/aidlc-pipeline/references/gate-rules.md`
- Create: `.claude/commands/aidlc.md`

**Interfaces:**
- Consumes: `config/personas.json`, `config/pipeline-models.json`, `progress-template.json`.
- Produces: the orchestration procedure every phase skill is invoked from; gate + loopback rules other skills reference by path.

- [ ] **Step 1: Write `.claude/skills/aidlc-pipeline/SKILL.md`:**

````markdown
---
name: aidlc-pipeline
description: Run the gated AIDLC pipeline for a ticket (requirements → tests-first → implement → review → security → local test → readiness → PR). Trigger with "run aidlc pipeline", "/aidlc", "work ticket <ID> through the pipeline".
---

# AIDLC Pipeline Orchestrator

## Inputs
`--ticket=<ID>` (required), `--resume`, `--from=<phase>`.
Ticket content: `docs/artifacts/<ID>/ticket.md`. If missing, ask the user to paste the ticket text and write that file first.

## Setup
1. Read `config/personas.json` and `config/pipeline-models.json`.
2. If `docs/artifacts/<ID>/progress.json` exists and `--resume`: continue from `checkpoint`. Otherwise copy `references/progress-template.json` → `docs/artifacts/<ID>/progress.json`, fill ticket/title/created.

## Phase 0 — Classify (haiku-tier task: keep it short)
Read ticket.md. Pick persona: BA (analysis/requirements-only ask), UI (page/component/visual), Backend (service/endpoint/data), BugFix (defect, non-a11y), A11yBugFix (accessibility defect). Write persona + phase_set (from personas.json) into progress.json; mark phases not in phase_set as "skipped". Confirm persona with the user via AskUserQuestion (options: detected persona first, alternatives after).

## Phase loop
For each phase in phase_set, in order:
1. Set phase status `in_progress`, stamp `started`, set `current_phase`, update PROGRESS.md.
2. Execute the phase:
   - 1 → Skill aidlc-requirements   - 2 → Skill aidlc-qa-generate
   - 3 → Skill aidlc-design          - 4 → implement per requirements.md + design.md + findings-*.md (DOER work happens in this session; follow CLAUDE.md conventions; make the phase-2 skeletons pass)
   - 5 → run `dotnet build ./eShopOnWeb.sln` then `dotnet test tests/UnitTests/UnitTests.csproj`; failures = findings, loop back per gate-rules
   - 6 → Skill aidlc-review (FRESH subagent)   - 7 → Skill aidlc-security (FRESH subagent)
   - 8 → Skill aidlc-local-test (only reachable when 5, 6, 7 all `done` with zero open findings)
   - 9 → Skill aidlc-readiness      - 10 → Skill aidlc-pr
3. Validate the phase artifact (exists, non-empty, matches the shape in docs/artifacts/README.md).
4. Record telemetry on the phase entry: model used, input_tokens, output_tokens, cost_usd (tokens × pricing from pipeline-models.json; for subagent phases use the usage reported by the Agent tool result; for in-session phases estimate from the work products and say so in PROGRESS.md).
5. Stamp `finished`, status `done` (or `error`), update PROGRESS.md.
6. Gate per `references/gate-rules.md`. STOP and wait for the user at every gate.

## Rules
- Phases 6 and 7 MUST run as subagents (Agent tool) with fresh context — pass them only: the git diff, requirements.md, testcases.md, and their skill instructions. Never pass this session's conversation.
- Loopback: any finding marked High at 6/7, or any test failure at 5/8, sets that phase to `error`, increments its `loopbacks`, and returns to phase 4 with the findings file as input. If a phase's `loopbacks` reaches 3: stop, tell the user, wait.
- Model routing: phases marked haiku in pipeline-models.json are mechanical — keep outputs terse; when run as subagents pass model "haiku".
- PROGRESS.md rendering: table of phases (icon ✅/🔄/⏭️/❌/⬜, name, started, finished, cost, loopbacks) + one-line current status + running total cost.
````

- [ ] **Step 2: Write `references/gate-rules.md`:**

```markdown
# Gate Rules

At every phase boundary, after validation:

1. Validation FAILED → present only: [Fix artifact and re-validate] [Abort pipeline]. Approve is suppressed.
2. Validation PASSED → AskUserQuestion: "Phase <n> (<name>) complete — <one-line artifact summary>. Proceed?"
   Options: [Approve → next phase] [Revise this phase] [Pause (checkpoint saved)] [Abort].
3. On Pause: write current_phase to `checkpoint` in progress.json; tell the user how to resume: `/aidlc --ticket=<ID> --resume`.
4. Phase 10 human gate is special: show readiness.md VERBATIM (confidence, AC coverage, risks) before asking. Only after Approve may the PR be generated.
5. Loopback (from gate-rules caller): findings-*.md is the input to the next phase-4 pass. Do not clear previous findings files; append a `## Round <n>` section.
```

- [ ] **Step 3: Write `.claude/commands/aidlc.md`:**

```markdown
---
description: Run the AIDLC pipeline for a ticket
---
Use the aidlc-pipeline skill with arguments: $ARGUMENTS
```

- [ ] **Step 4: Verify** — `.claude/skills/aidlc-pipeline/SKILL.md` frontmatter has `name: aidlc-pipeline`; gate-rules.md has 5 numbered rules.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/aidlc-pipeline/ .claude/commands/aidlc.md
git commit -m "feat(aidlc): orchestrator skill, /aidlc command, gate rules"
```

---

### Task 4: Requirements skill (phase 1)

**Files:**
- Create: `.claude/skills/aidlc-requirements/SKILL.md`

**Interfaces:**
- Consumes: `docs/artifacts/<ID>/ticket.md`, persona's `maxQuestions` from progress.json/personas.json.
- Produces: `docs/artifacts/<ID>/requirements.md` with numbered `AC-<n>` acceptance criteria — the contract phases 2, 4, 6, 7, 9 trace against.

- [ ] **Step 1: Write the skill:**

````markdown
---
name: aidlc-requirements
description: AIDLC phase 1 — interrogate a ticket into requirements and acceptance criteria (max 10 questions). Invoked by aidlc-pipeline; also usable standalone with "analyze requirements for <ticket>".
---

# Requirements Interrogation

## Procedure
1. Read `docs/artifacts/<ID>/ticket.md` and the repo's CLAUDE.md.
2. Draft your understanding: problem, affected components, unknowns.
3. Interrogate the human: AskUserQuestion, ONE question per message, multiple-choice where possible. HARD CAP: persona's maxQuestions (10 default, 5 for BugFix/A11yBugFix). Stop earlier when marginal value drops. Prioritize: scope boundaries > success criteria > edge cases > non-functionals (a11y, perf, security) > rollout.
4. Write `docs/artifacts/<ID>/requirements.md`:

```markdown
# Requirements — <ID>
## Problem statement
## In scope
## Out of scope
## Acceptance criteria
- AC-1: <testable statement>
- AC-2: ...
## Assumptions (unconfirmed)
## Affected components
<Clean Architecture layers + likely files>
## Q&A log
<numbered question → answer>
```

## Rules
- Every AC must be independently testable (a QA engineer could verify it without reading code).
- Record every question and answer in the Q&A log — the count proves the cap was respected.
- If the human's answers contradict the ticket, flag it in Assumptions rather than silently choosing.
````

- [ ] **Step 2: Verify** frontmatter name is `aidlc-requirements`; body contains "HARD CAP".

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/aidlc-requirements/
git commit -m "feat(aidlc): requirements interrogation skill (phase 1)"
```

---

### Task 5: PR skill (phase 10)

**Files:**
- Create: `.claude/skills/aidlc-pr/SKILL.md`

**Interfaces:**
- Consumes: `readiness.md`, `progress.json`, approved human gate.
- Produces: pushed branch + PR via `gh`, `cost-summary.json`, optional ADO comment.

- [ ] **Step 1: Write the skill:**

````markdown
---
name: aidlc-pr
description: AIDLC phase 10 — generate cost summary, open the PR, optionally post cost to ADO. Mechanical phase; keep output terse (haiku-tier). Invoked by aidlc-pipeline after the human approves readiness.
---

# PR + Cost Summary

## Preconditions
Phase 9 artifact approved by the human at the gate. Never run before that approval.

## Procedure
1. Write `docs/artifacts/<ID>/cost-summary.json`:
   `{ticket, persona, total_input_tokens, total_output_tokens, total_cost_usd, loopback_cost_usd, per_phase:[{phase,name,cost_usd}]}` — sums computed from progress.json phase entries; loopback_cost_usd = cost attributable to phases whose loopbacks > 0 beyond their first pass (best estimate, note method in PROGRESS.md).
2. Commit all artifact + code changes: `git add -A && git commit -m "<type>(<ID>): <title>"` (follow repo commit style).
3. Push branch; open PR with `gh pr create` — title `<type>(<ID>): <title>`, body: filled `.github/PULL_REQUEST_TEMPLATE.md` (check the AIDLC gates that actually ran; AI tool = "Claude Code AIDLC pipeline"), plus a `## Cost` section rendering cost-summary.json as a table.
4. ADO (optional): if `.claude/sdlc/ado.json` exists with `{org, project, workItemId}` AND env `AZURE_DEVOPS_PAT` is set, post the cost table as a work-item comment via `az boards work-item update --id <workItemId> --discussion "<table>"`. On ANY failure: warn in PROGRESS.md and continue — never fail the pipeline on ADO.
5. Mark phase 10 done in progress.json; final PROGRESS.md update with total cost line.
````

- [ ] **Step 2: Verify** body contains "Never fail the pipeline on ADO" (case-insensitive) and "cost-summary.json".

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/aidlc-pr/
git commit -m "feat(aidlc): PR + cost-summary skill (phase 10)"
```

---

## Wave 2 — Quality gates

### Task 6: QA-generate skill (phase 2)

**Files:**
- Create: `.claude/skills/aidlc-qa-generate/SKILL.md`

**Interfaces:**
- Consumes: `requirements.md` (AC-n list), persona's `localTest` mode.
- Produces: `testcases.md` + automation skeletons: xUnit files under `tests/UnitTests/...` (dotnet personas) or Playwright specs under `tests/UITests/` (playwright personas). Phase 4's definition of done = these pass; phase 8 runs them.

- [ ] **Step 1: Write the skill:**

````markdown
---
name: aidlc-qa-generate
description: AIDLC phase 2 — generate functional test cases and automation skeletons from acceptance criteria BEFORE implementation. Invoked by aidlc-pipeline.
---

# QA Generation (tests before code)

## Procedure
1. Read `docs/artifacts/<ID>/requirements.md`. Every AC-n gets at least one test case.
2. Write `docs/artifacts/<ID>/testcases.md`:

```markdown
# Test Cases — <ID>
## TC-1 (covers AC-1)
Given / When / Then
Type: unit|integration|ui|a11y   Automation: <file path or "manual">
...
## Traceability
| AC | TCs | Automated? |
```

3. Generate automation skeletons per persona localTest mode:
   - dotnet: xUnit + NSubstitute, one class per scenario, folder `tests/UnitTests/ApplicationCore/Services/<Service>Tests/` (copy style from BasketServiceTests/AddItemToBasket.cs). Body = arrange/act/assert with `Assert.Fail("pending implementation — <TC-id>")`.
   - playwright: spec files under `tests/UITests/<ID>/<tc-id>.spec.ts` using @playwright/test, `test.fixme()` marker until implementation.
   - playwright-axe: same plus `@axe-core/playwright` scan asserting zero serious/critical violations on the affected page.
   - none (BA persona): testcases.md only.
4. Skeletons MUST compile/lint but fail (red) — they are the phase-4 target.

## Rules
- No test case without an AC; no AC without a test case (traceability table proves it).
- Do not implement any production code in this phase.
````

- [ ] **Step 2: Verify** body contains "Traceability" and "before implementation" semantics (grep "BEFORE implementation").

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/aidlc-qa-generate/
git commit -m "feat(aidlc): upfront QA generation skill (phase 2)"
```

---

### Task 7: Review + security subagent skills (phases 6, 7)

**Files:**
- Create: `.claude/skills/aidlc-review/SKILL.md`
- Create: `.claude/skills/aidlc-security/SKILL.md`

**Interfaces:**
- Consumes (both): git diff of the ticket branch, `requirements.md`, `testcases.md` — NOTHING else from the doer session.
- Produces: `findings-review.md` / `findings-security.md`; finding line format `[High|Medium|Low] file:line — problem — suggested fix`; High findings trigger loopback per gate-rules.

- [ ] **Step 1: Write `.claude/skills/aidlc-review/SKILL.md`:**

````markdown
---
name: aidlc-review
description: AIDLC phase 6 — code review in a FRESH context. The orchestrator dispatches this as a subagent with only artifacts; it must not see the doer's conversation.
---

# Fresh-Context Code Review

You are a reviewer who did NOT write this code. Inputs you receive: the diff, requirements.md, testcases.md, CLAUDE.md.

## Checks
1. Every AC-n: implemented? Point to file:line evidence. Unimplemented AC = High finding.
2. Correctness: logic errors, null handling without Guard clauses, async misuse.
3. Conventions: Clean Architecture direction, Ardalis patterns, test style per CLAUDE.md.
4. Tests: phase-2 skeletons now pass and genuinely assert (a test emptied of asserts = High).

## Output — write `docs/artifacts/<ID>/findings-review.md` (append `## Round <n>` on loopbacks)
```markdown
## Round <n>
- [High|Medium|Low] <file>:<line> — <problem> — <suggested fix>
### AC coverage
| AC | Evidence (file:line) | Verdict |
### Summary
<count by severity> — CLEAN or FINDINGS
```
Verdict CLEAN requires zero High findings.
````

- [ ] **Step 2: Write `.claude/skills/aidlc-security/SKILL.md`:**

````markdown
---
name: aidlc-security
description: AIDLC phase 7 — security scan in a FRESH context. Dispatched as a subagent with only artifacts.
---

# Fresh-Context Security Scan

Inputs: the diff, requirements.md, CLAUDE.md. You did not write this code.

## Checks (scoped to the diff, OWASP-guided)
A01 access control (BuyerId always from authenticated context, [Authorize] on new endpoints) · A02 sensitive data in code/logs/responses · A03 injection (FromSqlRaw/ExecuteSqlRaw/string-built queries, Html.Raw) · A05 misconfiguration (CORS, exposed errors) · A07 CSRF/antiforgery on state-changing handlers · A09 swallowed exceptions / missing Warning-level logs on failure paths · secrets or connection strings committed.

## Output — write `docs/artifacts/<ID>/findings-security.md` (append `## Round <n>` on loopbacks)
Same finding format as review: `- [High|Medium|Low] file:line — problem — fix`, category headers per OWASP item checked ("No issues" where clean), and a final `### Summary` line: CLEAN or FINDINGS. High = loopback.
````

- [ ] **Step 3: Verify** both files contain "FRESH context" and the finding line format.

- [ ] **Step 4: Commit**

```bash
git add .claude/skills/aidlc-review/ .claude/skills/aidlc-security/
git commit -m "feat(aidlc): fresh-context review and security skills (phases 6-7)"
```

---

### Task 8: Readiness skill (phase 9)

**Files:**
- Create: `.claude/skills/aidlc-readiness/SKILL.md`

**Interfaces:**
- Consumes: all prior artifacts + latest findings files + test results.
- Produces: `readiness.md` — the exact document the human sees at the phase-10 gate.

- [ ] **Step 1: Write the skill:**

````markdown
---
name: aidlc-readiness
description: AIDLC phase 9 — confidence score, AC coverage, and risk assessment before the human PR gate. Invoked by aidlc-pipeline; also triggers on "readiness report" / "are we ready to ship".
---

# Readiness Report

Read requirements.md, testcases.md, findings-*.md (all rounds), progress.json.

## Write `docs/artifacts/<ID>/readiness.md`:
```markdown
# Readiness — <ID>
## Confidence score: <0-100>
<one line justification. Deductions: unimplemented/unevidenced AC −15 each; open Medium finding −5; any phase skipped that phase_set required −20; loopbacks ≥2 on any phase −5.>
## AC coverage
| AC | Implementation (file:line) | Test (TC / file) | Result |
## Risk register
| # | Risk | Likelihood | Impact | Mitigation |
## Open findings
<Medium/Low still open, from findings files — High cannot be open here>
## Recommendation
SHIP / SHIP WITH RISKS / DO NOT SHIP — <one line>
```

## Rules
- Never say SHIP with an open High finding or a failing test — that state must have looped back already; if you see it, say DO NOT SHIP and flag the pipeline bug.
- Scores are computed from the deduction table, not vibes; show the arithmetic.
````

- [ ] **Step 2: Verify** contains "Confidence score" and the deduction arithmetic rule.

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/aidlc-readiness/
git commit -m "feat(aidlc): readiness report skill (phase 9)"
```

---

## Wave 3 — UI + local testing

### Task 9: Local-test skill (phase 8) + Playwright scaffolding

**Files:**
- Create: `.claude/skills/aidlc-local-test/SKILL.md`
- Create: `tests/UITests/package.json`
- Create: `tests/UITests/playwright.config.ts`

**Interfaces:**
- Consumes: automation skeletons from phase 2 (now implemented), persona `localTest` mode, gate precondition (5, 6, 7 clean).
- Produces: `findings-localtest.md`; pass/fail drives loopback.

- [ ] **Step 1: Write the skill:**

````markdown
---
name: aidlc-local-test
description: AIDLC phase 8 — run generated functional automation and Playwright UI tests locally. ONLY runs after build+unit, code review, and security scan are all clean. Invoked by aidlc-pipeline.
---

# Local Testing (last gate before readiness)

## Precondition check (hard)
Read progress.json: phases 5, 6, 7 must be status `done` and both findings files' latest round must end CLEAN. If not, refuse and return control to the orchestrator — this phase must not run early.

## Procedure by persona localTest mode
- dotnet: `dotnet test ./eShopOnWeb.sln` (full suite, not just unit).
- playwright: start the app (`cd src/Web && dotnet run --launch-profile https` in background; wait for https://localhost:5001 to answer), then `cd tests/UITests && npx playwright test <ID>/`. Stop the app after.
- playwright-axe: same, specs include axe scans; any serious/critical violation = failure.
- none: mark phase skipped.

## Output
Write `docs/artifacts/<ID>/findings-localtest.md`: per-TC result table (TC, spec file, pass/fail, failure excerpt), `## Round <n>` on repeats, `### Summary` CLEAN or FINDINGS. Any failure → orchestrator loops back to phase 4. App-won't-start counts as FINDINGS, never as skip.
````

- [ ] **Step 2: Write `tests/UITests/package.json`:**

```json
{
  "name": "eshop-uitests",
  "private": true,
  "devDependencies": {
    "@playwright/test": "^1.49.0",
    "@axe-core/playwright": "^4.10.0"
  }
}
```

- [ ] **Step 3: Write `tests/UITests/playwright.config.ts`:**

```typescript
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  timeout: 30_000,
  retries: 0,
  use: {
    baseURL: 'https://localhost:5001',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  reporter: [['list'], ['json', { outputFile: 'results.json' }]],
});
```

- [ ] **Step 4: Verify** — skill contains "Precondition check (hard)"; `Get-Content tests/UITests/package.json | ConvertFrom-Json` parses.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/aidlc-local-test/ tests/UITests/
git commit -m "feat(aidlc): local-testing skill (phase 8) + Playwright scaffolding"
```

---

### Task 10: Design skill (phase 3)

**Files:**
- Create: `.claude/skills/aidlc-design/SKILL.md`

**Interfaces:**
- Consumes: `requirements.md`.
- Produces: `design.md` consumed by phase 4. Skipped automatically for BugFix/A11yBugFix (not in their phase_set).

- [ ] **Step 1: Write the skill:**

````markdown
---
name: aidlc-design
description: AIDLC phase 3 — design the change for a ticket (Backend/UI personas; bug-fix personas skip this). Invoked by aidlc-pipeline.
---

# Design

Read requirements.md and the affected components. Write `docs/artifacts/<ID>/design.md`:

```markdown
# Design — <ID>
## Approach
<2-3 paragraphs: chosen approach + one rejected alternative and why>
## Component changes
| Layer | File | Change |
## New public interfaces
<exact signatures phase 4 must produce>
## Data / migration impact
<EF Core changes; migrations are ALWAYS flagged for human review per CLAUDE.md>
## AC mapping
| AC | Design element |
```

Rules: respect Clean Architecture direction (ApplicationCore references nothing outward); prefer existing patterns (Guard clauses, specifications, Result<T>, MediatR events) over new ones; UI persona: include page/route and a11y notes (labels, focus, contrast).
````

- [ ] **Step 2: Verify** contains "AC mapping".

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/aidlc-design/
git commit -m "feat(aidlc): design skill (phase 3)"
```

---

## Wave 4 — Telemetry + safety

### Task 11: Safety hooks

**Files:**
- Create: `scripts/hooks/guard-secrets.ps1`
- Create: `scripts/hooks/guard-destructive.ps1`
- Create: `scripts/hooks/guard-env.ps1`
- Modify: `.claude/settings.json` (create if absent — check first; merge, don't clobber)

**Interfaces:**
- Produces: hook scripts exiting 0 (allow) or 2 with a reason on stderr (block), per Claude Code hooks contract.

- [ ] **Step 1: Write `scripts/hooks/guard-secrets.ps1`:**

```powershell
# UserPromptSubmit hook: block prompts carrying likely secrets/PII.
$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$p = $in.prompt
$patterns = @(
  'sk-ant-[A-Za-z0-9\-_]{20,}',                # Anthropic key
  'ghp_[A-Za-z0-9]{36}',                       # GitHub PAT
  'AKIA[0-9A-Z]{16}',                          # AWS access key
  'eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.',    # JWT
  '-----BEGIN (RSA |EC )?PRIVATE KEY-----',    # PEM
  '\b\d{3}-\d{2}-\d{4}\b'                      # SSN
)
foreach ($rx in $patterns) {
  if ($p -match $rx) {
    [Console]::Error.WriteLine("Blocked: prompt appears to contain a secret/PII (pattern: $rx). Redact and retry.")
    exit 2
  }
}
exit 0
```

- [ ] **Step 2: Write `scripts/hooks/guard-destructive.ps1`:**

```powershell
# PreToolUse(Bash) hook: block irreversible commands.
$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$cmd = $in.tool_input.command
$patterns = @(
  'rm\s+-rf\s+/(\s|$)', 'rm\s+-rf\s+~', 'mkfs\.', 'dd\s+if=.*of=/dev/',
  'git\s+push\s+.*--force(?!-with-lease)\s+.*\b(main|master)\b',
  ':\(\)\s*\{\s*:\|:&\s*\};:'
)
foreach ($rx in $patterns) {
  if ($cmd -match $rx) {
    [Console]::Error.WriteLine("Blocked destructive command (pattern: $rx).")
    exit 2
  }
}
exit 0
```

- [ ] **Step 3: Write `scripts/hooks/guard-env.ps1`:**

```powershell
# PreToolUse(Read|Edit|Write) hook: block access to .env files (allow .env.example/.sample/.template).
$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $in.tool_input.file_path
if ($path -match '(^|[\\/])\.env($|\.(?!example|sample|template)[^\\/]*$)') {
  [Console]::Error.WriteLine("Blocked: access to .env files is not allowed. Use .env.example for structure.")
  exit 2
}
exit 0
```

- [ ] **Step 4: Merge hooks into `.claude/settings.json`** (read existing file first; add `hooks` keys without removing existing settings):

```json
{
  "hooks": {
    "UserPromptSubmit": [
      { "hooks": [{ "type": "command", "command": "powershell -NoProfile -File scripts/hooks/guard-secrets.ps1" }] }
    ],
    "PreToolUse": [
      { "matcher": "Bash", "hooks": [{ "type": "command", "command": "powershell -NoProfile -File scripts/hooks/guard-destructive.ps1" }] },
      { "matcher": "Read|Edit|Write", "hooks": [{ "type": "command", "command": "powershell -NoProfile -File scripts/hooks/guard-env.ps1" }] }
    ]
  }
}
```

- [ ] **Step 5: Test each guard**

```powershell
'{"prompt":"my key is ghp_0123456789012345678901234567890123456789"}' | powershell -NoProfile -File scripts/hooks/guard-secrets.ps1; $LASTEXITCODE  # expect 2
'{"prompt":"hello world"}' | powershell -NoProfile -File scripts/hooks/guard-secrets.ps1; $LASTEXITCODE                                          # expect 0
'{"tool_input":{"command":"rm -rf /"}}' | powershell -NoProfile -File scripts/hooks/guard-destructive.ps1; $LASTEXITCODE                        # expect 2
'{"tool_input":{"file_path":"src/.env"}}' | powershell -NoProfile -File scripts/hooks/guard-env.ps1; $LASTEXITCODE                              # expect 2
'{"tool_input":{"file_path":".env.example"}}' | powershell -NoProfile -File scripts/hooks/guard-env.ps1; $LASTEXITCODE                          # expect 0
```

- [ ] **Step 6: Commit**

```bash
git add scripts/hooks/ .claude/settings.json
git commit -m "feat(aidlc): safety hooks (secrets, destructive commands, .env guard)"
```

---

### Task 12: ADO config template + gitignore

**Files:**
- Create: `.claude/sdlc/ado.json.example`
- Modify: `.gitignore` (append)

**Interfaces:**
- Produces: the optional ADO config contract `aidlc-pr` reads: `{org, project, workItemId}` at `.claude/sdlc/ado.json` (real file gitignored; PAT only ever via env `AZURE_DEVOPS_PAT`).

- [ ] **Step 1: Write `.claude/sdlc/ado.json.example`:**

```json
{
  "org": "https://dev.azure.com/<your-org>",
  "project": "<your-project>",
  "workItemId": 0
}
```

- [ ] **Step 2: Append to `.gitignore`:**

```
# AIDLC local config (never commit real ADO config; PAT comes from env AZURE_DEVOPS_PAT)
.claude/sdlc/ado.json
tests/UITests/node_modules/
tests/UITests/test-results/
tests/UITests/results.json
```

- [ ] **Step 3: Verify** — `git check-ignore .claude/sdlc/ado.json` prints the path.

- [ ] **Step 4: Commit**

```bash
git add .claude/sdlc/ado.json.example .gitignore
git commit -m "feat(aidlc): ADO config template; ignore local config and UI test output"
```

---

### Task 13: Dry-run ticket + end-to-end validation

**Files:**
- Create: `docs/artifacts/SAMPLE-1/ticket.md`

- [ ] **Step 1: Write the sample ticket:**

```markdown
# SAMPLE-1 — UriComposer returns template unchanged for null CatalogBaseUrl

## Description
When `CatalogSettings.CatalogBaseUrl` is null (misconfigured environment), `UriComposer.ComposePicUri` silently substitutes an empty string, producing broken image URLs at runtime with no log signal. Expected: a Guard clause that fails fast at construction, plus unit tests for UriComposer (currently zero).

Type: bug (Backend persona expected: BugFix)
```

- [ ] **Step 2: Run the pipeline:** `/aidlc --ticket=SAMPLE-1`. Expected persona: BugFix (confirm at phase-0 gate).

- [ ] **Step 3: Assert after the run** (this is the pipeline's acceptance test):
  - `docs/artifacts/SAMPLE-1/` contains progress.json, PROGRESS.md, requirements.md (Q&A log ≤5 questions), testcases.md (traceability table), findings-review.md, findings-security.md, findings-localtest.md, readiness.md, cost-summary.json.
  - progress.json parses; every phase in BugFix phase_set is `done` or `skipped`; phase 3 is `skipped`.
  - cost-summary.json `total_cost_usd` equals the sum of per-phase `cost_usd` (±0.01).
  - Loopback behavior observed at least once (if none occurred naturally, inject one: ask the reviewer subagent round to treat a missing test assert as High) and `loopbacks` incremented in progress.json.
  - New UriComposer tests pass: `dotnet test tests/UnitTests/UnitTests.csproj`.

- [ ] **Step 4: Commit artifacts and push**

```bash
git add docs/artifacts/SAMPLE-1/
git commit -m "test(aidlc): SAMPLE-1 end-to-end pipeline dry run artifacts"
git push
```
