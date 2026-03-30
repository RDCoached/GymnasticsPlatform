# Security Guardian Agent

Autonomous security monitoring and remediation using Claude Code agents.

## Responsibilities

The Security Guardian ensures application security by:

1. **Find Vulnerabilities** - SAST, DAST, dependency, and container scanning
2. **Auto-Fix Issues** - Automatically remediate CRITICAL/HIGH severity issues
3. **Automate Scanning** - Set up CI/CD security automation to reduce manual work
4. **Create Issues** - Document findings that need manual review
5. **Monitor Dependencies** - Track vulnerable packages and suggest updates

## Invocation

**Automatically spawned by Claude after committing code.**

The agent will:
- Scan changed files for security vulnerabilities
- Auto-fix CRITICAL/HIGH/MEDIUM issues (missing auth, CORS, SQL injection, secrets, rate limiting)
- Create GitHub issues only for LOW severity or complex vulnerabilities
- Set up CI/CD automation if missing (GitHub Actions security workflow)
- Run dependency scans (dotnet, npm, container images)
- Create PR with fixes and explanations
- **Wait for CI checks to complete** and verify all pass
- **Only declare success when CI is fully green** ✅

## What Gets Scanned

### Backend (.NET)
- **Authentication/Authorization** - Missing `[Authorize]`, weak JWT config
- **SQL Injection** - Raw SQL with string concatenation
- **Secrets** - Hardcoded passwords, API keys, connection strings
- **CORS** - `AllowAnyOrigin()`, missing restrictions
- **Dependencies** - Vulnerable NuGet packages
- **Rate Limiting** - Missing throttling on auth endpoints
- **Error Handling** - Information disclosure in error messages

### Frontend (React/TypeScript)
- **XSS** - `dangerouslySetInnerHTML`, unescaped user input
- **Token Storage** - localStorage vs sessionStorage vs httpOnly cookies
- **Dependencies** - Vulnerable npm packages
- **CSRF** - Missing anti-forgery protection
- **Input Validation** - Client-only validation (needs server-side too)

### Infrastructure
- **Container Images** - Vulnerable base images, outdated packages
- **CI/CD Pipeline** - Missing security scans, weak configurations
- **Secrets Management** - Exposed credentials, missing .gitignore

## Auto-Fix Capabilities

The agent automatically fixes CRITICAL, HIGH, and MEDIUM severity issues.

### CRITICAL (Fixed Automatically)

✅ **Hardcoded Secrets**
```csharp
// Before: Hardcoded password
var conn = "Server=prod;Password=secret123";

// After: From configuration
var conn = builder.Configuration.GetConnectionString("Default");
```

✅ **SQL Injection**
```csharp
// Before: String interpolation
db.ExecuteSqlRaw($"DELETE FROM Orders WHERE Id = {id}");

// After: Parameterized
db.ExecuteSqlRaw("DELETE FROM Orders WHERE Id = {0}", id);
```

### HIGH (Fixed Automatically)

✅ **Missing Authorization**
```csharp
// Before: No authorization
app.MapDelete("/api/users/{id}", DeleteUser);

// After: Authorization required
app.MapDelete("/api/users/{id}", DeleteUser)
    .RequireAuthorization();
```

✅ **Insecure CORS**
```csharp
// Before: Allows any origin
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin()));

// After: Explicit origins
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins)
     .WithMethods("GET", "POST", "PUT", "DELETE")
     .WithHeaders("Content-Type", "Authorization")));
```

### MEDIUM (Fixed Automatically)

✅ **Rate Limiting**
```csharp
// Before: No rate limiting
group.MapPost("/login", Login);

// After: Rate limiting added
builder.Services.AddRateLimiter(options => { /* config */ });
group.MapPost("/login", Login)
    .RequireRateLimiting("auth");
```

✅ **Security Headers**
```csharp
// Before: Missing headers
// (none)

// After: Security headers added
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    await next();
});
```

✅ **Token Storage**
```typescript
// Before: localStorage
localStorage.setItem('accessToken', token);

// After: sessionStorage (better security)
sessionStorage.setItem('accessToken', token);
```

### LOW (Issue Created Only)

For LOW severity issues, the agent creates a GitHub issue with:
- Detailed explanation
- Attack scenario
- Fix recommendation
- Code examples

Examples:
- Rate limiting needed
- Security headers missing
- Weak password policies
- Information disclosure in logs

## CI/CD Automation

**First Run**: If no security scanning detected, the agent creates:

`.github/workflows/security-scan.yml`:
- .NET dependency scan (`dotnet list package --vulnerable`)
- Frontend dependency scan (`npm audit`)
- Container scanning (Trivy)
- Secrets detection (TruffleHog)
- Runs on: push, PR, weekly schedule

**Benefits**:
- Catches vulnerabilities before they reach production
- Reduces agent workload (fewer manual scans needed)
- Blocks PRs with CRITICAL issues
- Automated weekly dependency checks

## Manual Invocation

Ask Claude anytime:
- "Run security scan"
- "Check for vulnerabilities"
- "Security review before PR"
- "Scan for secrets"

## Example Workflow

```
Developer: git commit -m "feat: add delete user endpoint"
           git push

[Security Guardian spawns in background]

Agent:
  🔍 Scanning changed files...
     - UserEndpoints.cs (new DELETE endpoint)

  ❌ Found 1 CRITICAL vulnerability:
     - Missing [Authorize] on DELETE /api/users/{id}

  🔧 Auto-fixing...
     ✅ Added RequireAuthorization() to endpoint

  🏗️ CI/CD Check...
     ⚠️  No security scanning in GitHub Actions
     ✅ Created .github/workflows/security-scan.yml

  📦 Creating PR...
     Branch: security/fix-missing-auth-20260330
     ✅ PR #47 created: "security: add authorization to delete endpoint"

  🧪 Verifying CI... (CRITICAL STEP)
     ⏳ Waiting for checks to start...
     ⏳ Backend Tests: Running...
     ⏳ Frontend Tests: Running...
     ⏳ Security Scan: Running...
     ✅ Backend Tests: Passed (233 tests)
     ✅ Frontend Tests: Passed (48 tests)
     ✅ Security Scan: No new vulnerabilities
     ✅ All checks passed - CI fully green

  ✅ Security scan complete - PR ready for review
     - 1 CRITICAL issue auto-fixed
     - CI/CD automation added
     - Ready for review and merge

Developer: [reviews PR, merges]
```

## Coordination with QA Agent

Both agents work together:

- **QA Agent**: Ensures test coverage and code quality
- **Security Agent**: Ensures security posture

They run independently after commits and can both create PRs on different branches.

## Configuration

### Customize Severity Thresholds

The agent uses these severity levels:
- **CRITICAL**: Direct exploit (auth bypass, SQL injection, hardcoded secrets)
- **HIGH**: Significant risk (missing authorization, XSS, data leaks)
- **MEDIUM**: Defense-in-depth (weak validation, information disclosure)
- **LOW**: Best practices (TODOs, commented secrets)

### Customize Auto-Fix Behavior

By default, the agent auto-fixes CRITICAL/HIGH/MEDIUM issues. To change:

Create `.claude/security-config.json`:
```json
{
  "autoFix": {
    "critical": true,
    "high": true,
    "medium": false,
    "low": false
  },
  "createIssues": {
    "critical": true,
    "high": true,
    "medium": true,
    "low": false
  },
  "cicdAutomation": {
    "enabled": true,
    "updateExisting": true
  }
}
```

## Security Scan Results

The agent generates comprehensive reports:

```markdown
# Security Scan Report

**Date**: 2026-03-30
**Commit**: abc1234
**Files Changed**: 5

## Summary
- CRITICAL: 0
- HIGH: 2 (auto-fixed)
- MEDIUM: 1 (issue created)
- LOW: 0

## Findings

### [HIGH] Missing Authorization
**Location**: UserEndpoints.cs:45
**Status**: ✅ Auto-fixed
**Fix**: Added RequireAuthorization()

### [HIGH] Insecure CORS
**Location**: Program.cs:122
**Status**: ✅ Auto-fixed
**Fix**: Changed AllowAnyOrigin() to WithOrigins()

### [MEDIUM] No Rate Limiting
**Location**: AuthEndpoints.cs
**Status**: 📋 Issue #123 created
**Recommendation**: Add rate limiting to login endpoint

## Actions Taken
- ✅ Auto-fixed 2 HIGH issues
- ✅ Created 1 GitHub issue
- ✅ Added CI/CD security workflow
- ✅ Created PR #47
```

## Best Practices

1. **Review Auto-Fixes**: Always review the PR before merging
2. **Address Issues Promptly**: Don't let security issues accumulate
3. **Keep Dependencies Updated**: Run `dotnet outdated` and `npm outdated` regularly
4. **Monitor CI Results**: Check weekly security scan results
5. **Rotate Secrets**: Use Azure Key Vault or similar for production

## FAQ

**Q: Will it break my code?**
A: No. The agent runs tests locally before creating a PR, then waits for CI to pass. It only declares success when all checks are green.

**Q: Can I disable auto-fix?**
A: Yes, create `.claude/security-config.json` and set `autoFix` levels to false.

**Q: Does it replace manual security reviews?**
A: No. It handles common issues automatically but complex vulnerabilities need human expertise.

**Q: How often does it scan?**
A: After every commit. Plus weekly via CI/CD automation.

**Q: What if it creates false positives?**
A: The agent learns from corrections. If you close an issue as "not a vulnerability", it will remember the pattern.

## Related Documentation

- [dotnet-security-guardian skill](/Users/rdcoached/.claude/skills/dotnet-security-guardian/SKILL.md)
- [QA Agent](./QA-AGENT.md)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)

---

Generated by Security Guardian Agent
Last Updated: 2026-03-30
