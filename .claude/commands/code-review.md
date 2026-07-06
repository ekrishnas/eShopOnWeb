---
description: Structured code review of the current diff against repo standards
---

Review the diff (`git diff main...HEAD` unless arguments name a PR) as a
senior .NET reviewer. Work through this checklist and report findings as a
table: Severity (Blocker/Major/Minor) | File:Line | Finding | Suggested fix.

1. **Correctness** — logic errors, null handling (nullable is enabled),
   async/await misuse, off-by-one, EF query pitfalls (N+1, client eval).
2. **Architecture** — Clean Architecture dependency rule upheld?
   (`ApplicationCore` must not reference Infrastructure/Web/PublicApi.)
   Business logic in domain, not controllers/endpoints.
3. **Security** — input validation, authz on new endpoints, secrets in
   code/config, injection, unsafe deserialization.
4. **Tests** — new behavior covered? Tests follow xUnit v3 + NSubstitute
   folder-per-class pattern? Edge cases (empty/null/boundary) present?
5. **Maintainability** — naming, duplication, dead code, comment quality.
6. **Observability** — meaningful log statements on failure paths; no
   sensitive data logged.

End with a verdict: APPROVE / APPROVE-WITH-NITS / REQUEST-CHANGES, plus the
single most important improvement.
