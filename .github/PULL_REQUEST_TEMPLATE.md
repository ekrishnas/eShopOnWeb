## What Changed

<!-- One paragraph: what does this PR do and why? -->

## Type of Change

- [ ] Bug fix
- [ ] New feature
- [ ] Refactor / code quality
- [ ] Test coverage
- [ ] Security improvement
- [ ] CI/CD / build improvement
- [ ] Documentation

## AIDLC Gates

### AI Assistance
- [ ] AI tool used (Claude Code / OpenAI Codex / other): `___________`
- [ ] AI-generated code has been read and understood by a human
- [ ] AI suggestions that were rejected or modified: <!-- briefly note why -->

### Code Quality
- [ ] No new compiler warnings introduced
- [ ] Guard clauses added for new public method parameters
- [ ] Clean Architecture dependency direction preserved (ApplicationCore has no Infrastructure/Web refs)
- [ ] No secrets, connection strings, or credentials committed

### Testing
- [ ] Unit tests added or updated for changed logic
- [ ] Integration / functional tests updated if behaviour changed
- [ ] All tests pass locally: `dotnet test ./eShopOnWeb.sln`
- [ ] Test coverage not regressed

### Security
- [ ] No SQL injection risk (EF Core parameterisation used, no raw string queries)
- [ ] No XSS risk (Razor auto-encodes; any `Html.Raw` usage is intentional and safe)
- [ ] No IDOR risk (user-scoped queries filter by authenticated `BuyerId`)
- [ ] New endpoints require authentication/authorisation where appropriate

### Release Readiness
- [ ] Change is backwards-compatible OR migration path is documented
- [ ] No EF Core migration left un-generated for schema changes
- [ ] Observability not degraded (structured logging added for new flows)
- [ ] Rollback: this change can be reverted without data loss

## Files Changed

<!-- Key files and why they changed -->

| File | Change summary |
|---|---|
| | |

## Test Evidence

```
# Paste dotnet test output or link to CI run
```

## Risk / Open Items

<!-- Anything reviewers should watch for, or known gaps -->
