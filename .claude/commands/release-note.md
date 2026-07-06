---
description: Produce a release-readiness note for the current branch
---

Generate `docs/aidlc/release-readiness.md` content for the current branch
using exactly this template (fill every row from real command output, not
memory — run the builds/tests/scans if evidence is missing):

| Area | Status |
|---|---|
| Change summary | <1–3 sentences> |
| Build status | <dotnet build result> |
| Test status | <suite-by-suite pass counts> |
| Security/quality checks | <CodeQL, Gitleaks, NuGetAudit, dependency review outcomes> |
| Observability impact | <new/changed logs, metrics, failure signals> |
| Rollback consideration | <how to revert; data/config implications> |
| Human review required | <what a human must still verify> |
