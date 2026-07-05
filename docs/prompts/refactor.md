# Prompt: Refactor

Use this when a piece of code is working but needs improvement — duplication, poor naming, missing abstraction, or violation of the project's patterns.

---

## Prompt

```
Read CLAUDE.md first.

Refactor `[DESCRIBE WHAT: the method / class / file]` at `[FILE PATH]`.

The problem with the current code:
[DESCRIBE THE SMELL: e.g., "The method is 80 lines long and mixes repository access with business logic", or "The same null-checking pattern is duplicated in 4 service methods"]

Constraints (do not violate these):
- Do not change the public interface (method signatures and return types must stay the same)
- Do not change test behaviour — all existing tests must still pass
- Keep the Clean Architecture dependency rules: ApplicationCore cannot reference Infrastructure or Web
- Use Ardalis Guard clauses for input validation, not raw if/throw blocks
- Follow the existing patterns: Guard clauses at method entry, domain events via MediatR, Result<T> for methods that can fail for domain reasons

Proposed approach (adjust if you see a better way):
[DESCRIBE YOUR IDEA OR LEAVE BLANK]

Steps I want you to follow:
1. Read the current code carefully
2. Identify all callers of the code being changed (grep for usages)
3. Propose the refactored version with an explanation of what changed and why
4. List any risks or assumptions
5. Wait for my approval before writing the changes

Do not write code until I confirm the approach.
```

---

## When to Use

- Before a feature that touches a messy area of the code
- When the same logic has been duplicated across more than 2 places
- As a follow-up to a code review that flagged maintainability issues

## Related Prompts

- `code-review.md` — review the refactored output
- `test-generation.md` — add tests before refactoring (safer)
