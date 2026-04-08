# Dual-Provider Testing Guide

## Overview

This document provides comprehensive testing procedures for validating both Keycloak and Microsoft Entra ID authentication providers. The goal is to ensure feature parity, identify edge cases, and verify no regressions when switching between providers.

## Pre-Testing Setup

### Environment Requirements

- **Staging environment** with access to both providers
- **Test user accounts** created in both Keycloak and Entra ID
- **Monitoring tools** configured (Seq, Grafana, or Application Insights)
- **Load testing tools** (k6, JMeter, or Artillery)

### Configuration Files

Create two configuration profiles:

**appsettings.Keycloak.json**
```json
{
  "Authentication": {
    "ActiveProvider": "Keycloak",
    "Keycloak": {
      "Authority": "https://keycloak.staging.example.com/realms/gymnastics",
      "Audience": "user-portal",
      "AdminApiUrl": "https://keycloak.staging.example.com",
      "Realm": "gymnastics",
      "ClientId": "admin-cli"
    }
  }
}
```

**appsettings.EntraId.json**
```json
{
  "Authentication": {
    "ActiveProvider": "EntraId",
    "EntraId": {
      "TenantId": "{your-tenant-id}",
      "ApiClientId": "{api-client-id}",
      "ApiClientSecret": "{from-key-vault}",
      "Instance": "https://login.microsoftonline.com/",
      "Audience": "api://gymnastics-api",
      "ExtensionAppId": "{extension-app-id}",
      "TenantIdExtensionAttributeName": "extension_{app-id}_tenant_id"
    }
  }
}
```

### Frontend Configuration

**Keycloak (.env.keycloak)**
```bash
VITE_AUTH_PROVIDER=keycloak
VITE_KEYCLOAK_URL=https://keycloak.staging.example.com
VITE_KEYCLOAK_REALM=gymnastics
VITE_KEYCLOAK_CLIENT_ID=user-portal
VITE_API_URL=https://api.staging.example.com
```

**Entra ID (.env.entra)**
```bash
VITE_AUTH_PROVIDER=entra
VITE_ENTRA_CLIENT_ID={spa-client-id}
VITE_ENTRA_TENANT_ID={tenant-id}
VITE_REDIRECT_URI=https://app.staging.example.com/auth/callback
VITE_API_URL=https://api.staging.example.com
```

## Test Scenarios Matrix

### Core Authentication Flows

| Scenario | Keycloak | Entra ID | Priority | Notes |
|----------|----------|----------|----------|-------|
| Email/password signup | ✅ Test | ✅ Test | P0 | Verify email verification sent |
| Email/password login | ✅ Test | ✅ Test | P0 | Session cookie must be set |
| Google OAuth signup | ✅ Test | ✅ Test | P0 | Check federation redirect flow |
| Google OAuth login | ✅ Test | ✅ Test | P0 | Verify token contains tenant_id |
| Microsoft OAuth signup | ❌ N/A | ✅ Test | P1 | Entra-only feature |
| Microsoft OAuth login | ❌ N/A | ✅ Test | P1 | Entra-only feature |
| Logout (email/password) | ✅ Test | ✅ Test | P0 | Cookie cleared, session invalidated |
| Logout (OAuth) | ✅ Test | ✅ Test | P0 | MSAL/Keycloak cache cleared |

### Token Management

| Scenario | Keycloak | Entra ID | Priority | Notes |
|----------|----------|----------|----------|-------|
| Token refresh (silent) | ✅ Test | ✅ Test | P0 | Should happen automatically before expiry |
| Token refresh (expired) | ✅ Test | ✅ Test | P1 | Fallback to interactive login |
| Token validation (valid) | ✅ Test | ✅ Test | P0 | API accepts valid Bearer token |
| Token validation (invalid) | ✅ Test | ✅ Test | P0 | API returns 401 Unauthorized |
| Token validation (expired) | ✅ Test | ✅ Test | P0 | API returns 401, client refreshes |
| Cross-origin token requests | ✅ Test | ✅ Test | P1 | CORS headers correct |

### User Management

| Scenario | Keycloak | Entra ID | Priority | Notes |
|----------|----------|----------|----------|-------|
| Get current user (/api/auth/me) | ✅ Test | ✅ Test | P0 | Returns userId, email, tenantId, roles |
| Email verification flow | ✅ Test | ✅ Test | P1 | Email sent, link works, account activated |
| Password reset initiation | ✅ Test | ✅ Test | P1 | Email sent with reset link |
| Password reset completion | ✅ Test | ✅ Test | P1 | New password accepted, can login |
| Duplicate email registration | ✅ Test | ✅ Test | P0 | Returns 409 Conflict |
| User profile update | ✅ Test | ✅ Test | P1 | Full name updated in both provider and DB |

### Multi-Tenancy

| Scenario | Keycloak | Entra ID | Priority | Notes |
|----------|----------|----------|----------|-------|
| New user tenant assignment | ✅ Test | ✅ Test | P0 | Onboarding tenant (all 0s) set correctly |
| Tenant ID in JWT claims | ✅ Test | ✅ Test | P0 | Custom claim: tenant_id present |
| Tenant update via onboarding | ✅ Test | ✅ Test | P0 | UpdateUserTenantIdAsync called |
| Tenant isolation (DbContext) | ✅ Test | ✅ Test | P0 | User A cannot see User B's data |
| Tenant resolution middleware | ✅ Test | ✅ Test | P0 | HttpContext.Items["TenantId"] set |

### Role-Based Access Control

| Scenario | Keycloak | Entra ID | Priority | Notes |
|----------|----------|----------|----------|-------|
| Coach role assignment | ✅ Test | ✅ Test | P0 | UserRole record created |
| Gymnast role assignment | ✅ Test | ✅ Test | P0 | UserRole record created |
| Platform Admin role | ✅ Test | ✅ Test | P1 | Has access to admin endpoints |
| Role verification in JWT | ✅ Test | ✅ Test | P0 | JWT contains role claims |
| Unauthorized access (no role) | ✅ Test | ✅ Test | P0 | Returns 403 Forbidden |
| Unauthorized access (wrong role) | ✅ Test | ✅ Test | P0 | Returns 403 Forbidden |

## Test Procedures

### Manual Test Script Template

For each scenario, follow this structure:

```markdown
#### Test: [Scenario Name]
**Provider:** [Keycloak | Entra ID]
**Priority:** [P0 | P1]
**Prerequisites:** [Any setup required]

**Steps:**
1. [Action to perform]
2. [Expected result]
3. [Verification step]

**Actual Result:**
- [ ] Pass
- [ ] Fail - [description]

**Notes:**
[Any observations, performance data, etc.]

**Tested By:** [Your name]
**Date:** [YYYY-MM-DD]
```

### Example: Email/Password Login Test

```markdown
#### Test: Email/Password Login
**Provider:** Keycloak
**Priority:** P0
**Prerequisites:** User account exists with email=test@example.com, password=Test123!

**Steps:**
1. Navigate to https://app.staging.example.com/sign-in
2. Enter email: test@example.com, password: Test123!
3. Click "Sign In"
4. Verify redirect to /dashboard
5. Open DevTools → Application → Cookies
6. Verify session cookie "gymnastics_session" is set with HttpOnly flag
7. Verify cookie domain matches app domain
8. Make API call to /api/auth/me
9. Verify response contains { userId, email, tenantId, roles }

**Actual Result:**
- [x] Pass

**Notes:**
- Login took 1.2 seconds
- Session cookie expires in 7 days
- TenantId correctly set to onboarding tenant (00000000-0000-0000-0000-000000000001)

**Tested By:** John Doe
**Date:** 2026-04-08
```

## Performance Testing

### Metrics to Capture

For each provider, capture the following metrics:

**Authentication Latency**
- P50 (median) login time
- P95 login time
- P99 login time

**Token Operations**
- Silent token refresh success rate
- Interactive token refresh rate
- Token validation time (server-side)

**Failure Rates**
- Authentication failures / total attempts
- Token refresh failures / total refreshes
- API call failures (401/403) / total API calls

**Throughput**
- Concurrent logins supported (100, 500, 1000 users)
- Requests per second (RPS) for /api/auth/me
- Time to first byte (TTFB) for OAuth callbacks

### Load Test Scripts

#### k6 Script: Concurrent Logins

```javascript
// load-test-login.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Ramp up to 50 users
    { duration: '3m', target: 50 },   // Stay at 50 users
    { duration: '1m', target: 100 },  // Ramp up to 100 users
    { duration: '3m', target: 100 },  // Stay at 100 users
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p95<2000', 'p99<5000'], // 95% under 2s, 99% under 5s
    http_req_failed: ['rate<0.05'],              // Failure rate under 5%
  },
};

export default function () {
  const payload = JSON.stringify({
    email: `user-${__VU}-${__ITER}@example.com`,
    password: 'Test123!',
  });

  const params = {
    headers: { 'Content-Type': 'application/json' },
  };

  const loginRes = http.post('https://api.staging.example.com/api/auth/login', payload, params);

  check(loginRes, {
    'login status is 200': (r) => r.status === 200,
    'access token received': (r) => r.json('accessToken') !== undefined,
    'tenant ID present': (r) => r.json('user.tenantId') !== undefined,
  });

  sleep(1);
}
```

**Run Command:**
```bash
# Test Keycloak
k6 run --env PROVIDER=keycloak load-test-login.js

# Test Entra ID
k6 run --env PROVIDER=entra load-test-login.js
```

## Configuration Validation

### Pre-Test Checklist

**Backend Configuration (Keycloak)**
- [ ] `Authentication:ActiveProvider` = "Keycloak"
- [ ] `Authentication:Keycloak:Authority` points to correct realm
- [ ] `Authentication:Keycloak:Audience` matches client ID
- [ ] Keycloak realm has test users created
- [ ] Keycloak Google identity provider configured (if testing OAuth)

**Backend Configuration (Entra ID)**
- [ ] `Authentication:ActiveProvider` = "EntraId"
- [ ] `Authentication:EntraId:TenantId` is correct tenant GUID
- [ ] `Authentication:EntraId:ApiClientId` matches API app registration
- [ ] `Authentication:EntraId:ApiClientSecret` loaded from Key Vault
- [ ] Extension attribute configured in Entra ID app
- [ ] Google external identity provider configured (if testing OAuth)

**Frontend Configuration (Keycloak)**
- [ ] `VITE_AUTH_PROVIDER` = "keycloak"
- [ ] `VITE_KEYCLOAK_URL` points to Keycloak instance
- [ ] `VITE_KEYCLOAK_CLIENT_ID` matches client ID in realm
- [ ] CORS allowed origins include frontend URL

**Frontend Configuration (Entra ID)**
- [ ] `VITE_AUTH_PROVIDER` = "entra"
- [ ] `VITE_ENTRA_CLIENT_ID` matches SPA app registration
- [ ] `VITE_ENTRA_TENANT_ID` is correct tenant GUID
- [ ] `VITE_REDIRECT_URI` is registered in app registration
- [ ] CORS allowed origins include frontend URL

**Database**
- [ ] Database contains test users with ProviderUserId from both Keycloak and Entra
- [ ] UserRoles table has role assignments for test users
- [ ] Multiple tenants exist for multi-tenancy tests

**Monitoring**
- [ ] Seq/Serilog configured and receiving logs
- [ ] OpenTelemetry exporter configured (if using)
- [ ] Application Insights connected (if using Azure)

### Configuration Validation Script

```bash
#!/bin/bash
# validate-config.sh

echo "=== Backend Configuration Validation ==="
echo ""

# Check appsettings.json
if [ -f "appsettings.json" ]; then
  ACTIVE_PROVIDER=$(jq -r '.Authentication.ActiveProvider' appsettings.json)
  echo "Active Provider: $ACTIVE_PROVIDER"

  if [ "$ACTIVE_PROVIDER" = "Keycloak" ]; then
    AUTHORITY=$(jq -r '.Authentication.Keycloak.Authority' appsettings.json)
    AUDIENCE=$(jq -r '.Authentication.Keycloak.Audience' appsettings.json)
    echo "  Keycloak Authority: $AUTHORITY"
    echo "  Keycloak Audience: $AUDIENCE"
  elif [ "$ACTIVE_PROVIDER" = "EntraId" ]; then
    TENANT_ID=$(jq -r '.Authentication.EntraId.TenantId' appsettings.json)
    CLIENT_ID=$(jq -r '.Authentication.EntraId.ApiClientId' appsettings.json)
    echo "  Entra Tenant ID: $TENANT_ID"
    echo "  Entra Client ID: $CLIENT_ID"
  else
    echo "  ⚠️  Unknown provider: $ACTIVE_PROVIDER"
  fi
else
  echo "❌ appsettings.json not found"
fi

echo ""
echo "=== Frontend Configuration Validation ==="
echo ""

# Check .env file
if [ -f "frontend/user-portal/.env" ]; then
  FE_PROVIDER=$(grep "^VITE_AUTH_PROVIDER=" frontend/user-portal/.env | cut -d'=' -f2)
  echo "Frontend Provider: $FE_PROVIDER"

  if [ "$FE_PROVIDER" = "keycloak" ]; then
    KC_URL=$(grep "^VITE_KEYCLOAK_URL=" frontend/user-portal/.env | cut -d'=' -f2)
    echo "  Keycloak URL: $KC_URL"
  elif [ "$FE_PROVIDER" = "entra" ]; then
    ENTRA_CLIENT=$(grep "^VITE_ENTRA_CLIENT_ID=" frontend/user-portal/.env | cut -d'=' -f2)
    echo "  Entra Client ID: $ENTRA_CLIENT"
  fi
else
  echo "❌ frontend/user-portal/.env not found"
fi

echo ""
echo "=== Configuration Consistency Check ==="
echo ""

if [ "$ACTIVE_PROVIDER" = "$FE_PROVIDER" ] ||
   [ "$ACTIVE_PROVIDER" = "Keycloak" -a "$FE_PROVIDER" = "keycloak" ] ||
   [ "$ACTIVE_PROVIDER" = "EntraId" -a "$FE_PROVIDER" = "entra" ]; then
  echo "✅ Backend and frontend providers match"
else
  echo "❌ Backend provider ($ACTIVE_PROVIDER) does not match frontend provider ($FE_PROVIDER)"
fi
```

**Usage:**
```bash
chmod +x validate-config.sh
./validate-config.sh
```

## Switching Between Providers

### Quick Switch Procedure

**Backend: Keycloak → Entra ID**

1. Stop the API server
2. Update `appsettings.json`:
   ```bash
   # Using jq
   jq '.Authentication.ActiveProvider = "EntraId"' appsettings.json > tmp.json && mv tmp.json appsettings.json

   # Or manually edit
   vim appsettings.json
   # Change: "ActiveProvider": "EntraId"
   ```
3. Verify Entra ID configuration section is complete
4. Start the API server
5. Check logs for startup message: `"Active authentication provider: EntraId"`

**Backend: Entra ID → Keycloak**

1. Stop the API server
2. Update `appsettings.json`:
   ```bash
   jq '.Authentication.ActiveProvider = "Keycloak"' appsettings.json > tmp.json && mv tmp.json appsettings.json
   ```
3. Verify Keycloak configuration section is complete
4. Start the API server
5. Check logs for startup message: `"Active authentication provider: Keycloak"`

**Frontend: Keycloak → Entra ID**

1. Stop the dev server
2. Copy Entra configuration:
   ```bash
   cp frontend/user-portal/.env.entra frontend/user-portal/.env
   ```
3. Rebuild frontend:
   ```bash
   cd frontend/user-portal
   npm run build
   ```
4. Start dev server or deploy built files

**Frontend: Entra ID → Keycloak**

1. Stop the dev server
2. Copy Keycloak configuration:
   ```bash
   cp frontend/user-portal/.env.keycloak frontend/user-portal/.env
   ```
3. Rebuild frontend:
   ```bash
   cd frontend/user-portal
   npm run build
   ```
4. Start dev server or deploy built files

## Edge Cases to Test

### Authentication Edge Cases

1. **User exists in Keycloak but not in database**
   - Expected: Registration flow creates UserProfile record
   - Test with: Fresh Keycloak user

2. **User exists in database but not in Entra ID**
   - Expected: Login fails with 401 Unauthorized
   - Test with: Deleted Entra user

3. **Email case sensitivity**
   - Expected: Emails are case-insensitive (test@example.com == TEST@example.com)
   - Test with: Same email in different cases

4. **Concurrent login from multiple devices**
   - Expected: Both sessions remain active independently
   - Test with: Login from Chrome, then Firefox

5. **Token refresh during API call**
   - Expected: Silent refresh happens, API call succeeds
   - Test with: Expired token + valid refresh token

6. **OAuth state mismatch**
   - Expected: Login fails with security error
   - Test with: Manipulated OAuth state parameter

### Multi-Tenancy Edge Cases

1. **User switches tenant during session**
   - Expected: New tenant_id claim in refreshed token
   - Test with: UpdateUserTenantIdAsync → logout → login

2. **Tenant ID missing from JWT**
   - Expected: Fallback to database lookup
   - Test with: Manually crafted token without tenant_id claim

3. **User assigned to deleted tenant**
   - Expected: Graceful error, redirect to onboarding
   - Test with: DELETE tenant, then login as user

### Provider-Specific Edge Cases

**Keycloak-Specific**
1. Google OAuth with unverified email
2. Keycloak realm disabled mid-session
3. Client secret rotated (old token still valid)

**Entra-Specific**
1. Extension attribute not yet propagated (eventual consistency)
2. Microsoft OAuth with work/school account (not consumer)
3. Conditional Access policy blocking login (wrong location/device)

## Test Results Template

After completing all tests, fill out this summary:

```markdown
# Dual-Provider Testing Results

**Test Date:** [YYYY-MM-DD]
**Tested By:** [Name]
**Environment:** [Staging URL]

## Test Summary

| Provider | Scenarios Tested | Passed | Failed | Notes |
|----------|------------------|--------|--------|-------|
| Keycloak | 0 / 45 | 0 | 0 | |
| Entra ID | 0 / 45 | 0 | 0 | |

## Performance Comparison

| Metric | Keycloak | Entra ID | Winner |
|--------|----------|----------|--------|
| P50 Login Latency | 0ms | 0ms | |
| P95 Login Latency | 0ms | 0ms | |
| P99 Login Latency | 0ms | 0ms | |
| Token Refresh Success Rate | 0% | 0% | |
| Max Concurrent Users | 0 | 0 | |

## Issues Found

### Keycloak Issues
- [ ] Issue #1: [Description]
- [ ] Issue #2: [Description]

### Entra ID Issues
- [ ] Issue #1: [Description]
- [ ] Issue #2: [Description]

### Parity Issues
- [ ] Feature X works in Keycloak but not Entra ID
- [ ] Feature Y works in Entra ID but not Keycloak

## Recommendations

- [ ] Proceed to Phase 6 (Production Migration)
- [ ] Fix critical issues before migration
- [ ] Additional testing required in areas: [list]

## Sign-Off

- [ ] All P0 scenarios passing
- [ ] Performance acceptable (<2s P95 login latency)
- [ ] No data loss observed
- [ ] No security issues found

**Approved By:** [Name]
**Date:** [YYYY-MM-DD]
```

## Troubleshooting

### Common Issues

**Issue: "Invalid redirect URI" in Entra ID**
- Cause: Redirect URI not registered in SPA app
- Fix: Add exact URL to app registration → Authentication → Redirect URIs

**Issue: "CORS policy" error in browser**
- Cause: Frontend origin not allowed in backend CORS policy
- Fix: Add origin to `builder.Services.AddCors()` in Program.cs

**Issue: "Extension attribute not found" in JWT**
- Cause: Optional claims not configured in app registration
- Fix: App registration → Token configuration → Add optional claim

**Issue: "Token signature validation failed"**
- Cause: Wrong authority or audience in JWT validation config
- Fix: Verify `options.Authority` and `options.Audience` match provider

**Issue: Session cookie not set after login**
- Cause: `credentials: 'include'` missing in fetch call
- Fix: Add to all API calls in api-client.ts

**Issue: Token refresh fails with "invalid_grant"**
- Cause: Refresh token expired or revoked
- Fix: Force user to interactive login again

## Observability Queries

### Seq Queries (Serilog)

**Authentication failures by provider**
```
Level = "Warning" AND MessageTemplate like "%authentication failed%"
| group by Provider
| count()
```

**Average login duration**
```
MessageTemplate = "User {ProviderUserId} authenticated successfully"
| extend Duration = DateDiff('millisecond', @Timestamp, PreviousTimestamp)
| summarize Avg(Duration) by Provider
```

**Token refresh rate**
```
MessageTemplate like "%token refresh%"
| bucket by 1m
| count()
```

### Application Insights Queries (KQL)

**P95 authentication latency**
```kusto
customMetrics
| where name == "auth.authentication.duration"
| summarize percentile(value, 95) by tostring(customDimensions.provider)
```

**Authentication failure rate**
```kusto
let total = customMetrics | where name == "auth.authentication.attempts" | count;
let failures = customMetrics | where name == "auth.authentication.failures" | count;
print FailureRate = todouble(failures) / todouble(total) * 100
```

## Next Steps

After completing Phase 5 testing:

1. **Document findings** in test results template
2. **Fix critical issues** before proceeding
3. **Review with stakeholders** (QA, DevOps, Security)
4. **Proceed to Phase 6** (Production Migration Planning) if all P0 tests pass
5. **Archive test results** for compliance/audit trail

---

**Document Version:** 1.0
**Last Updated:** 2026-04-08
**Maintained By:** Platform Team
