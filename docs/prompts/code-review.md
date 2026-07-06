# Prompt: Code Review

Use this prompt with Claude Code after completing a change. Paste it as-is, then replace `[DESCRIBE CHANGE]` with a one-liner.

---

## Prompt

```
Read CLAUDE.md first so you understand this codebase's architecture and conventions.

I've just made a change: [DESCRIBE CHANGE]

Please review the current git diff (`git diff main`) for the following:

**Correctness**
- Logic errors or off-by-one issues
- Unhandled null/empty cases that Ardalis Guard clauses should cover
- Missing exception handling for expected failure paths
- Any violation of Clean Architecture dependency direction (ApplicationCore must not reference Infrastructure or Web)

**Security**
- SQL injection risk (should be impossible via EF Core, but check for raw SQL or string interpolation in queries)
- XSS risk (check any use of Html.Raw or unencoded output in Razor)
- IDOR risk (does any query use a user-supplied ID without filtering by the authenticated BuyerId?)
- Sensitive data (secrets, credentials, PII) committed to code or logs

**Test Coverage**
- Is there a unit test for the changed logic?
- Are edge cases (null input, empty collection, boundary values) covered?
- Does the test follow the naming convention in CLAUDE.md (one class per scenario, folder under tests/UnitTests/...)?

**Code Quality**
- Does new code follow existing patterns (Guard clauses, Result<T>, MediatR for events)?
- Are there magic strings or numbers that should be constants?
- Is logging added for new flows at an appropriate level (Information for normal, Warning for handled errors)?

For each issue found: give the file path, line number, the problem, and a concrete fix.
If nothing needs changing in a category, say so explicitly.
```

---

## When to Use

- Before creating a pull request
- After an AI tool generated a significant block of code
- When reviewing someone else's PR with AI assistance

## Related Prompts

- `security-review.md` — deeper security focus
- `pre-commit-review.md` — lightweight quick check before committing
