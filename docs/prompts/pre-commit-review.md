# Prompt: Pre-Commit Quick Check

Lightweight review before every commit. Fast — targets the diff only, not the whole codebase.

---

## Prompt

```
Read CLAUDE.md for context.

Run `git diff --staged` and review only what I'm about to commit.

Quick checks (answer Yes/No for each):
1. Any hardcoded secrets, API keys, passwords, or connection strings?
2. Any TODO or FIXME comments that should be issues instead of committed code?
3. Any unused using statements or dead code introduced?
4. Any obvious null-reference risk (method called on a potentially null value without a Guard clause)?
5. Clean Architecture preserved? (No new references from ApplicationCore to Infrastructure or Web?)
6. Any test file removed or test commented out without a clear reason?
7. Any new NuGet package added? If so, name it — I'll check for CVEs.

For each "Yes": show the file, line, and what needs fixing.
For "No" answers: just list them as ✓.

This should take under 60 seconds. Do not perform a full review — only what's in the staged diff.
```

---

## When to Use

- Before every `git commit` when AI tools have been used in the session
- As a habit — treat it as a lint pass with semantic awareness

## Tip

Set this up as a git hook prompt in your workflow:
```bash
# In .claude/settings.json hooks, or as a manual habit:
# Run the pre-commit-review prompt in Claude Code before git commit
```

## Related Prompts

- `code-review.md` — full review before PR
- `security-review.md` — security-focused deep dive
