# Prompt: Security Review

Deeper security-focused review for a change or a specific file. Run before merging anything that touches auth, data access, or API endpoints.

---

## Prompt

```
Read CLAUDE.md first.

Perform a security review of [DESCRIBE SCOPE: the current git diff / the file at PATH / the endpoint NAME].

Check for each of the following OWASP Top 10 categories that are relevant to an ASP.NET Core e-commerce app:

**A01 — Broken Access Control**
- Do all new endpoints have [Authorize] or [AllowAnonymous] intentionally set?
- Does any query return data for user B when user A's ID is supplied? (Check that BuyerId always comes from HttpContext.User, never from the request body for user-scoped operations)
- Are admin-only endpoints protected with role checks?

**A02 — Cryptographic Failures**
- Are any passwords, tokens, or sensitive values stored in plaintext (logs, DB columns, config files)?
- Is any sensitive data returned in API responses that shouldn't be?

**A03 — Injection**
- Is there any raw SQL string concatenation or interpolation? (EF Core parameterises by default, but check for FromSqlRaw or ExecuteSqlRaw)
- Is there any shell command execution using user input?

**A05 — Security Misconfiguration**
- Are CORS policies overly permissive (AllowAnyOrigin)?
- Are error details (stack traces) exposed to end users in production paths?

**A06 — Vulnerable and Outdated Components**
- Are any NuGet packages added at a version with known CVEs? (Check NVD or GitHub Advisory Database)

**A07 — Identification and Authentication Failures**
- Are new user-facing endpoints that mutate state protected against CSRF? (ASP.NET Core Razor Pages includes AntiForgery by default — confirm it's not disabled)
- Are JWT tokens validated correctly in PublicApi (algorithm, expiry, audience)?

**A09 — Security Logging and Monitoring Failures**
- Are authentication failures logged?
- Are unexpected exceptions in business logic logged at Warning or Error level (not silently swallowed)?

For each issue: file path, line number, risk level (High/Medium/Low), description, and recommended fix.
If a category is not applicable to this change, say so in one line.
```

---

## When to Use

- Before merging any change to PublicApi endpoints
- After modifying authentication or authorisation logic
- When a dependency version is bumped
- As part of the AIDLC release-readiness checklist

## Related Prompts

- `code-review.md` — broader quality + correctness review
