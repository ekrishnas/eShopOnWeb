# AI Agent Workflows (CI-side) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Implementation subagents must use model `sonnet` (user directive).**

**Goal:** Two GitHub Actions workflows — AI PR review (sticky comment + labels) and label-triggered issue implementation (draft PR) — driven by headless Claude Code CLI.

**Architecture:** Workflows do all GitHub mechanics with `gh` CLI; Claude Code CLI (`claude -p`) does only the reasoning. Prompts are versioned templates in `docs/prompts/` with `{PLACEHOLDER}` tokens substituted at runtime with python3 (present on ubuntu-latest).

**Tech Stack:** GitHub Actions, `@anthropic-ai/claude-code` (npm), `gh` CLI (preinstalled on runners), python3 for prompt assembly.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-05-ai-agent-workflows-design.md` — follow exactly.
- Secret `ANTHROPIC_API_KEY` must exist in repo settings; workflows must degrade gracefully (comment + exit 0) when the Claude call fails.
- The agent never merges. Issue workflow opens **draft** PRs only.
- Diff scope: `*.cs *.csproj *.yml *.yaml`, truncated at 800 lines with a visible note.
- Labels created idempotently: `ai-reviewed` (purple #8250df), `ai-blocked` (red #d73a4a), `ai-skip`, `ai-review-requested`, `ai-implement`, `ai-drafted`, `ai-needs-human`.
- Branch: work continues on `feature/aidlc-enhancements`. Commit after every task.
- No test framework applies to YAML/markdown; each task's verification is a structural check plus (final task) a `workflow_dispatch` dry-run checklist.

---

### Task 1: PR-review prompt template

**Files:**
- Create: `docs/prompts/pr-review-agent.md`

**Interfaces:**
- Produces: template with placeholders `{CLAUDE_MD}`, `{PR_TITLE}`, `{PR_BODY}`, `{DIFF}`, `{TRUNCATION_NOTE}` — consumed by Task 3's python substitution step.

- [ ] **Step 1: Write the file** with this exact content:

````markdown
# Prompt: PR Review Agent (CI)

Used by `.github/workflows/ai-pr-review.yml`. Placeholders are substituted at runtime. Humans may also paste this into Claude Code manually, replacing placeholders by hand.

---

## Prompt

```
You are a senior .NET engineer reviewing a pull request on eShopOnWeb.

## Repo context (CLAUDE.md)
{CLAUDE_MD}

## PR metadata
Title: {PR_TITLE}
Description: {PR_BODY}

## Diff
{TRUNCATION_NOTE}
{DIFF}

## Instructions
Produce a review with EXACTLY this structure and nothing else:

## AI Review — eShopOnWeb

### Summary
2-3 plain-English sentences: what changed and why it appears to have changed.

### Impact
Which Clean Architecture layers are touched (ApplicationCore / Infrastructure / Web / PublicApi / BlazorAdmin). Flag any dependency-direction violation (ApplicationCore must not reference Infrastructure or Web).

### Code Review
Findings as a bulleted list. Each: `file:line — [High|Medium|Low] — problem — suggested fix`. Check: correctness, missing Guard clauses, security (raw SQL, Html.Raw, BuyerId scoping, secrets), test gaps vs the repo's one-class-per-scenario xUnit+NSubstitute convention. If a category is clean, write "No issues found" for it.

### Verdict
Exactly one line, one of:
✅ Looks good — <reason>
⚠️ Needs attention — <reason>
🚫 Do not merge — <reason>
```

## When to Use

- Automatically on every non-draft PR (CI).
- Manually before opening a PR: replace placeholders with your diff.

## Related Prompts

- `code-review.md` — interactive full review
- `issue-implement-agent.md` — CI issue implementation
````

- [ ] **Step 2: Verify structure**

Run: `grep -c "{CLAUDE_MD}\|{PR_TITLE}\|{PR_BODY}\|{DIFF}\|{TRUNCATION_NOTE}" docs/prompts/pr-review-agent.md`
Expected: `5`

- [ ] **Step 3: Commit**

```bash
git add docs/prompts/pr-review-agent.md
git commit -m "feat: add CI PR-review agent prompt template"
```

---

### Task 2: Issue-implement prompt template

**Files:**
- Create: `docs/prompts/issue-implement-agent.md`

**Interfaces:**
- Produces: template with placeholders `{CLAUDE_MD}`, `{ISSUE_TITLE}`, `{ISSUE_BODY}` — consumed by Task 4's analysis pass.

- [ ] **Step 1: Write the file** with this exact content:

````markdown
# Prompt: Issue Implementation Agent (CI) — Analysis Pass

Used by `.github/workflows/ai-issue-implement.yml`. Produces a plan only; a second pass implements it.

---

## Prompt

```
You are a senior .NET engineer implementing a GitHub issue on eShopOnWeb.

## Repo context (CLAUDE.md)
{CLAUDE_MD}

## Issue
Title: {ISSUE_TITLE}
Body: {ISSUE_BODY}

## Instructions
Do NOT write any code. Output only:

### Files to change
One bullet per file: exact path — what changes and why.

### Risks and ambiguities
Anything unclear in the issue, assumptions you are forced to make, or blast radius concerns. If the issue cannot be implemented safely without human input, say so explicitly on a line starting with "CANNOT-IMPLEMENT:".

### Implementation plan
Numbered steps. Each step small and verifiable. Include which tests to add or update, following the repo convention (xUnit + NSubstitute, one class per scenario under tests/UnitTests/...).
```

## When to Use

- Automatically when an issue is labeled `ai-implement`.
- Manually to scope an issue before picking it up.

## Related Prompts

- `pr-review-agent.md` — CI PR review
- `test-generation.md` — test conventions detail
````

- [ ] **Step 2: Verify structure**

Run: `grep -c "{CLAUDE_MD}\|{ISSUE_TITLE}\|{ISSUE_BODY}" docs/prompts/issue-implement-agent.md`
Expected: `3`

- [ ] **Step 3: Commit**

```bash
git add docs/prompts/issue-implement-agent.md
git commit -m "feat: add CI issue-implementation agent prompt template"
```

---

### Task 3: ai-pr-review.yml workflow

**Files:**
- Create: `.github/workflows/ai-pr-review.yml`

**Interfaces:**
- Consumes: `docs/prompts/pr-review-agent.md` (Task 1), repo `CLAUDE.md`.
- Produces: sticky PR comment (header `ai-review`), labels `ai-reviewed`/`ai-blocked`.

- [ ] **Step 1: Write the workflow** with this exact content:

```yaml
name: AI PR Review

on:
  pull_request:
    types: [opened, synchronize, reopened]
  workflow_dispatch:
    inputs:
      pr_number:
        description: 'PR number to review'
        required: true

permissions:
  contents: read
  pull-requests: write

concurrency:
  group: ai-review-${{ github.event.pull_request.number || inputs.pr_number }}
  cancel-in-progress: true

jobs:
  review:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    # Skip drafts, the agent's own ai/* branches (unless opted in), and ai-skip.
    if: >
      github.event_name == 'workflow_dispatch' ||
      (github.event.pull_request.draft == false &&
       (!startsWith(github.event.pull_request.head.ref, 'ai/') ||
        contains(github.event.pull_request.labels.*.name, 'ai-review-requested')) &&
       !contains(github.event.pull_request.labels.*.name, 'ai-skip'))
    env:
      GH_TOKEN: ${{ github.token }}
    steps:
      - name: Resolve PR number
        id: pr
        run: echo "number=${{ github.event.pull_request.number || inputs.pr_number }}" >> "$GITHUB_OUTPUT"

      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Checkout PR head (dispatch runs)
        if: github.event_name == 'workflow_dispatch'
        run: gh pr checkout ${{ steps.pr.outputs.number }}

      - name: Install Claude Code CLI
        run: npm install -g @anthropic-ai/claude-code

      - name: Build scoped diff (800-line cap)
        id: diff
        run: |
          git fetch origin main
          git diff origin/main...HEAD -- '*.cs' '*.csproj' '*.yml' '*.yaml' > full.diff
          LINES=$(wc -l < full.diff)
          head -n 800 full.diff > diff.txt
          if [ "$LINES" -gt 800 ]; then
            echo "note=NOTE: diff truncated to 800 of $LINES lines." >> "$GITHUB_OUTPUT"
          else
            echo "note=" >> "$GITHUB_OUTPUT"
          fi

      - name: Assemble prompt
        env:
          PR_NUM: ${{ steps.pr.outputs.number }}
          PR_TITLE: ${{ github.event.pull_request.title }}
          PR_BODY: ${{ github.event.pull_request.body }}
          TRUNCATION_NOTE: ${{ steps.diff.outputs.note }}
        run: |
          python3 - <<'EOF'
          import os, re
          tpl = open('docs/prompts/pr-review-agent.md').read()
          # extract the fenced prompt block
          prompt = re.search(r'## Prompt\n\n```\n(.*?)\n```', tpl, re.S).group(1)
          subs = {
            '{CLAUDE_MD}': open('CLAUDE.md').read(),
            '{PR_TITLE}': os.environ.get('PR_TITLE', f"PR #{os.environ.get('PR_NUM','')}"),
            '{PR_BODY}': os.environ.get('PR_BODY', '') or '(no description)',
            '{DIFF}': open('diff.txt').read(),
            '{TRUNCATION_NOTE}': os.environ.get('TRUNCATION_NOTE', ''),
          }
          for k, v in subs.items():
            prompt = prompt.replace(k, v)
          open('prompt.txt', 'w').write(prompt)
          EOF

      - name: Run AI review
        id: ai
        continue-on-error: true
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: claude -p "$(cat prompt.txt)" --allowedTools "" > review.md

      - name: Fallback comment on AI failure
        if: steps.ai.outcome == 'failure'
        run: printf '## AI Review — eShopOnWeb\n\n_AI review unavailable for this run (agent error or missing ANTHROPIC_API_KEY). CI is unaffected._\n' > review.md

      - name: Post sticky review comment
        uses: marocchino/sticky-pull-request-comment@v2
        with:
          header: ai-review
          number: ${{ steps.pr.outputs.number }}
          recreate: true
          path: review.md

      - name: Ensure labels exist
        run: |
          gh label create ai-reviewed --color 8250df --description "Reviewed by AI agent" --force
          gh label create ai-blocked --color d73a4a --description "AI verdict: do not merge" --force
          gh label create ai-skip --color ededed --description "Opt out of AI review" --force
          gh label create ai-review-requested --color 8250df --description "Opt ai/* draft PR into AI review" --force

      - name: Apply labels
        run: |
          gh pr edit ${{ steps.pr.outputs.number }} --add-label ai-reviewed
          if grep -q "🚫 Do not merge" review.md; then
            gh pr edit ${{ steps.pr.outputs.number }} --add-label ai-blocked
          else
            gh pr edit ${{ steps.pr.outputs.number }} --remove-label ai-blocked || true
          fi
```

- [ ] **Step 2: Structural verification**

Run: `grep -c "sticky-pull-request-comment@v2\|continue-on-error\|cancel-in-progress" .github/workflows/ai-pr-review.yml`
Expected: `3`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ai-pr-review.yml
git commit -m "feat: add AI PR review workflow (headless Claude Code)"
```

---

### Task 4: ai-issue-implement.yml workflow

**Files:**
- Create: `.github/workflows/ai-issue-implement.yml`

**Interfaces:**
- Consumes: `docs/prompts/issue-implement-agent.md` (Task 2), repo `CLAUDE.md`.
- Produces: branch `ai/issue-<N>-<slug>`, draft PR, issue labels `ai-drafted`/`ai-needs-human`.

- [ ] **Step 1: Write the workflow** with this exact content:

```yaml
name: AI Issue Implement

on:
  issues:
    types: [labeled]
  workflow_dispatch:
    inputs:
      issue_number:
        description: 'Issue number to implement'
        required: true

permissions:
  contents: write
  pull-requests: write
  issues: write

jobs:
  implement:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    if: github.event_name == 'workflow_dispatch' || github.event.label.name == 'ai-implement'
    env:
      GH_TOKEN: ${{ github.token }}
    steps:
      - name: Resolve issue number
        id: issue
        run: echo "number=${{ github.event.issue.number || inputs.issue_number }}" >> "$GITHUB_OUTPUT"

      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Install Claude Code CLI
        run: npm install -g @anthropic-ai/claude-code

      - name: Read issue
        id: read
        run: |
          gh issue view ${{ steps.issue.outputs.number }} --json title,body > issue.json
          TITLE=$(python3 -c "import json;print(json.load(open('issue.json'))['title'])")
          echo "title=$TITLE" >> "$GITHUB_OUTPUT"
          SLUG=$(python3 -c "import json,re;t=json.load(open('issue.json'))['title'];print('-'.join(re.sub(r'[^a-z0-9 ]','',t.lower()).split()[:5]))")
          echo "slug=$SLUG" >> "$GITHUB_OUTPUT"

      - name: Analysis pass
        id: analysis
        continue-on-error: true
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: |
          python3 - <<'EOF'
          import json, re
          tpl = open('docs/prompts/issue-implement-agent.md').read()
          prompt = re.search(r'## Prompt\n\n```\n(.*?)\n```', tpl, re.S).group(1)
          issue = json.load(open('issue.json'))
          prompt = prompt.replace('{CLAUDE_MD}', open('CLAUDE.md').read())
          prompt = prompt.replace('{ISSUE_TITLE}', issue['title'])
          prompt = prompt.replace('{ISSUE_BODY}', issue['body'] or '(no body)')
          open('prompt.txt', 'w').write(prompt)
          EOF
          claude -p "$(cat prompt.txt)" --allowedTools "" > plan.md
          if grep -q "CANNOT-IMPLEMENT:" plan.md; then echo "blocked=true" >> "$GITHUB_OUTPUT"; fi

      - name: Ensure labels exist
        run: |
          gh label create ai-implement --color 0e8a16 --description "Request AI implementation" --force
          gh label create ai-drafted --color 8250df --description "AI opened a draft PR" --force
          gh label create ai-needs-human --color d73a4a --description "AI could not implement" --force

      - name: Bail out if analysis failed or blocked
        if: steps.analysis.outcome == 'failure' || steps.analysis.outputs.blocked == 'true'
        run: |
          { printf 'AI agent could not implement this issue automatically.\n\n'; cat plan.md 2>/dev/null || true; } > comment.md
          gh issue comment ${{ steps.issue.outputs.number }} --body-file comment.md
          gh issue edit ${{ steps.issue.outputs.number }} --add-label ai-needs-human
          exit 0

      - name: Create branch
        if: steps.analysis.outcome == 'success' && steps.analysis.outputs.blocked != 'true'
        run: git checkout -b "ai/issue-${{ steps.issue.outputs.number }}-${{ steps.read.outputs.slug }}"

      - name: Implementation pass (file tools only, no shell)
        if: steps.analysis.outcome == 'success' && steps.analysis.outputs.blocked != 'true'
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
        run: |
          claude -p "Implement the following plan in this repository. Follow CLAUDE.md conventions. Plan:

          $(cat plan.md)" \
            --allowedTools "Edit" "Write" "Read" "Glob" "Grep" \
            --disallowedTools "Bash" \
            --permission-mode acceptEdits || true

      - name: Commit, push, open draft PR — or report no changes
        if: steps.analysis.outcome == 'success' && steps.analysis.outputs.blocked != 'true'
        run: |
          N=${{ steps.issue.outputs.number }}
          if [ -z "$(git status --porcelain)" ]; then
            { printf 'AI agent produced no file changes. Analysis plan for a human:\n\n'; cat plan.md; } > comment.md
            gh issue comment "$N" --body-file comment.md
            gh issue edit "$N" --add-label ai-needs-human
            exit 0
          fi
          git config user.name "ai-agent[bot]"
          git config user.email "ai-agent[bot]@users.noreply.github.com"
          git add -A
          git commit -m "ai: implement #$N — ${{ steps.read.outputs.title }}"
          git push -u origin HEAD
          { printf 'Closes #%s\n\n> [!WARNING]\n> AI-generated draft — requires human review before marking ready.\n\n## Analysis plan\n\n' "$N"; cat plan.md; } > prbody.md
          PR_URL=$(gh pr create --draft --title "ai: ${{ steps.read.outputs.title }}" --body-file prbody.md)
          gh issue comment "$N" --body "Draft PR opened: $PR_URL — please review before merging."
          gh issue edit "$N" --add-label ai-drafted
```

- [ ] **Step 2: Structural verification**

Run: `grep -c "gh pr create --draft\|ai-needs-human\|disallowedTools" .github/workflows/ai-issue-implement.yml`
Expected: at least `4`

- [ ] **Step 3: Commit and push**

```bash
git add .github/workflows/ai-issue-implement.yml
git commit -m "feat: add AI issue-implementation workflow (draft PRs)"
git push
```

---

### Task 5: Live dry-run checklist (human + agent)

**Files:** none (operational verification)

- [ ] **Step 1:** Confirm `ANTHROPIC_API_KEY` secret exists: `gh secret list --repo ekrishnas/eShopOnWeb` (ask the user to add it via repo Settings → Secrets if missing — the agent must not handle the key value).
- [ ] **Step 2:** Trigger review on an existing PR: `gh workflow run ai-pr-review.yml -f pr_number=<N> --repo ekrishnas/eShopOnWeb`, then `gh run watch`. Expected: sticky "AI Review — eShopOnWeb" comment + `ai-reviewed` label.
- [ ] **Step 3:** File a trivial test issue ("Add unit test for UriComposer.ComposePicUri"), label it `ai-implement`. Expected: branch `ai/issue-<N>-...`, draft PR, `ai-drafted` label.
- [ ] **Step 4:** Record outcomes (run URLs, comment screenshots) in the PR description of `feature/aidlc-enhancements`.
```
