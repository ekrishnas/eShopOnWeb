---
description: Security review of the current diff (OWASP-aligned)
---

Analyze the diff (`git diff main...HEAD`) strictly for security impact:

- **Secrets** — hardcoded keys/passwords/tokens (also check test fixtures
  and appsettings); config values that should be env vars.
- **Injection** — SQL (raw EF SQL?), command, LDAP, XSS in Razor
  (`Html.Raw`?), open redirects.
- **AuthN/AuthZ** — new endpoints: `[Authorize]`/policy applied? JWT
  validation params weakened? Cookie flags changed?
- **Data exposure** — PII in logs, verbose error responses, overly broad
  CORS.
- **Dependencies** — new packages: known advisories? (`dotnet list package
  --vulnerable --include-transitive`).
- **Crypto** — weak algorithms, short keys (<256-bit symmetric), ECB.

Report: Risk (High/Med/Low) | Location | Issue | Exploit scenario |
Remediation. State explicitly which categories came back clean.
