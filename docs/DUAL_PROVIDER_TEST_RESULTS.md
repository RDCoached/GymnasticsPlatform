# Dual-Provider Testing Results

**Test Date:** [YYYY-MM-DD]
**Tested By:** [Name]
**Environment:** [Staging URL]
**Backend Version:** [Commit SHA]
**Frontend Version:** [Commit SHA]

## Executive Summary

| Provider | Scenarios Tested | Passed | Failed | Skipped | Pass Rate |
|----------|------------------|--------|--------|---------|-----------|
| Keycloak | 0 / 45 | 0 | 0 | 0 | 0% |
| Entra ID | 0 / 45 | 0 | 0 | 0 | 0% |

**Overall Status:** 🔴 Not Started / 🟡 In Progress / 🟢 Complete

---

## Core Authentication Flows

### Email/Password Signup

**Keycloak**
- [ ] Registration form submits successfully
- [ ] Verification email sent
- [ ] Email contains valid verification link
- [ ] Clicking link activates account
- [ ] Can log in after activation
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Registration form submits successfully
- [ ] Verification email sent
- [ ] Email contains valid verification link
- [ ] Clicking link activates account
- [ ] Can log in after activation
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Email/Password Login

**Keycloak**
- [ ] Login form submits with valid credentials
- [ ] Session cookie set (HttpOnly flag)
- [ ] Redirects to dashboard
- [ ] /api/auth/me returns correct user data
- [ ] TenantId correctly set in response
- **Latency (P95):** _____ ms
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Redirects to Entra ID login page
- [ ] Enters credentials on Entra page
- [ ] Redirects back to /auth/callback
- [ ] Session cookie set (HttpOnly flag)
- [ ] Redirects to dashboard
- [ ] /api/auth/me returns correct user data
- [ ] TenantId correctly set in response
- **Latency (P95):** _____ ms
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Google OAuth Signup

**Keycloak**
- [ ] "Sign in with Google" button works
- [ ] Redirects to Google OAuth consent screen
- [ ] Redirects back after Google approval
- [ ] Account created in database
- [ ] TenantId set to onboarding tenant
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] "Sign in with Google" button works
- [ ] Redirects to Entra ID (federates to Google)
- [ ] Redirects to Google OAuth consent screen
- [ ] Redirects back through Entra → app
- [ ] Account created in database
- [ ] TenantId set to onboarding tenant
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Google OAuth Login

**Keycloak**
- [ ] Existing Google user can sign in
- [ ] No duplicate account created
- [ ] Correct tenant context loaded
- [ ] Session persists across page refresh
- **Latency (P95):** _____ ms
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Existing Google user can sign in
- [ ] No duplicate account created
- [ ] Correct tenant context loaded
- [ ] Session persists across page refresh
- **Latency (P95):** _____ ms
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Microsoft OAuth Signup

**Keycloak**
- [ ] N/A - Feature not supported
- **Result:** ⏭️ Skipped
- **Notes:** Microsoft OAuth only available with Entra ID provider

**Entra ID**
- [ ] "Sign in with Microsoft" button works
- [ ] Redirects to Microsoft OAuth consent screen
- [ ] Redirects back to /auth/callback
- [ ] Account created in database
- [ ] TenantId set to onboarding tenant
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Microsoft OAuth Login

**Keycloak**
- [ ] N/A - Feature not supported
- **Result:** ⏭️ Skipped
- **Notes:** Microsoft OAuth only available with Entra ID provider

**Entra ID**
- [ ] Existing Microsoft user can sign in
- [ ] No duplicate account created
- [ ] Correct tenant context loaded
- [ ] Session persists across page refresh
- **Latency (P95):** _____ ms
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Logout (Email/Password)

**Keycloak**
- [ ] Logout endpoint clears session cookie
- [ ] Backend session invalidated
- [ ] Cannot access protected endpoints after logout
- [ ] Redirects to sign-in page
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Logout endpoint clears session cookie
- [ ] Backend session invalidated
- [ ] MSAL cache cleared (if OAuth)
- [ ] Cannot access protected endpoints after logout
- [ ] Redirects to sign-in page
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Logout (OAuth)

**Keycloak**
- [ ] Logout clears Keycloak session
- [ ] Session cookie cleared
- [ ] Redirects to Keycloak logout URL
- [ ] Redirects back to sign-in page
- [ ] Cannot re-authenticate without credentials
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Logout clears MSAL cache
- [ ] Session cookie cleared
- [ ] Redirects to Entra logout URL
- [ ] Redirects back to sign-in page
- [ ] Cannot re-authenticate without credentials
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

---

## Token Management

### Token Refresh (Silent)

**Keycloak**
- [ ] Access token auto-refreshes before expiry
- [ ] No user interaction required
- [ ] Refresh happens in background
- [ ] API calls succeed during refresh
- **Refresh Success Rate:** _____ %
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Access token auto-refreshes before expiry
- [ ] MSAL acquireTokenSilent succeeds
- [ ] No user interaction required
- [ ] API calls succeed during refresh
- **Refresh Success Rate:** _____ %
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Token Refresh (Expired)

**Keycloak**
- [ ] Expired token triggers interactive login
- [ ] User redirected to login page
- [ ] Login succeeds
- [ ] New token issued
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Expired token triggers acquireTokenPopup
- [ ] MSAL popup opens
- [ ] User authenticates in popup
- [ ] New token issued
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Token Validation (Valid)

**Keycloak**
- [ ] Valid Bearer token accepted by API
- [ ] Request returns 200 OK
- [ ] Claims correctly parsed
- [ ] TenantId extracted from token
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Valid Bearer token accepted by API
- [ ] Request returns 200 OK
- [ ] Claims correctly parsed
- [ ] TenantId extracted from extension attribute
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Token Validation (Invalid)

**Keycloak**
- [ ] Invalid token returns 401 Unauthorized
- [ ] Error message clear
- [ ] No sensitive data leaked
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Invalid token returns 401 Unauthorized
- [ ] Error message clear
- [ ] No sensitive data leaked
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Token Validation (Expired)

**Keycloak**
- [ ] Expired token returns 401 Unauthorized
- [ ] Client triggers refresh flow
- [ ] New token obtained
- [ ] Original request retried successfully
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Expired token returns 401 Unauthorized
- [ ] Client triggers acquireTokenSilent
- [ ] New token obtained
- [ ] Original request retried successfully
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

---

## User Management

### Get Current User (/api/auth/me)

**Keycloak**
- [ ] Returns 200 OK with user data
- [ ] Response includes: userId, email, name, tenantId, roles
- [ ] TenantId matches database record
- [ ] Roles array populated correctly
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Returns 200 OK with user data
- [ ] Response includes: userId, email, name, tenantId, roles
- [ ] TenantId matches database record
- [ ] Roles array populated correctly
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Email Verification Flow

**Keycloak**
- [ ] Verification email sent on registration
- [ ] Email contains valid link
- [ ] Link expires after 24 hours
- [ ] Clicking link activates account
- [ ] Cannot login before verification (if enforced)
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Verification email sent on registration
- [ ] Email contains valid link
- [ ] Link expires after 24 hours
- [ ] Clicking link activates account
- [ ] Cannot login before verification (if enforced)
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Password Reset Initiation

**Keycloak**
- [ ] Password reset form submits
- [ ] Reset email sent to user
- [ ] Email contains reset link
- [ ] Link expires after set period
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Password reset form submits
- [ ] Reset email sent to user
- [ ] Email contains reset link
- [ ] Link expires after set period
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Password Reset Completion

**Keycloak**
- [ ] Reset link opens password form
- [ ] New password accepted
- [ ] Password strength validated
- [ ] Can login with new password
- [ ] Old password no longer works
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Reset link opens password form
- [ ] New password accepted
- [ ] Password strength validated
- [ ] Can login with new password
- [ ] Old password no longer works
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Duplicate Email Registration

**Keycloak**
- [ ] Second registration attempt with same email blocked
- [ ] Returns 409 Conflict
- [ ] Error message clear and actionable
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Second registration attempt with same email blocked
- [ ] Returns 409 Conflict
- [ ] Error message clear and actionable
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

---

## Multi-Tenancy

### New User Tenant Assignment

**Keycloak**
- [ ] New user assigned to onboarding tenant
- [ ] TenantId = 00000000-0000-0000-0000-000000000001
- [ ] User attribute updated in Keycloak
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] New user assigned to onboarding tenant
- [ ] TenantId = 00000000-0000-0000-0000-000000000001
- [ ] Extension attribute set in Entra ID
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Tenant ID in JWT Claims

**Keycloak**
- [ ] JWT contains tenant_id claim
- [ ] Claim value matches database record
- [ ] Claim survives token refresh
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] JWT contains extension_xxx_tenant_id claim
- [ ] Claim value matches database record
- [ ] Claim survives token refresh
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Tenant Update via Onboarding

**Keycloak**
- [ ] Onboarding flow updates tenant in database
- [ ] UpdateUserTenantIdAsync called
- [ ] User attribute updated in Keycloak
- [ ] Forced re-authentication
- [ ] New JWT has updated tenant_id
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Onboarding flow updates tenant in database
- [ ] UpdateUserTenantIdAsync called
- [ ] Extension attribute updated in Entra ID
- [ ] Forced re-authentication
- [ ] New JWT has updated tenant_id
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Tenant Isolation (DbContext)

**Keycloak**
- [ ] User A cannot see User B's data (different tenants)
- [ ] Global query filter active
- [ ] API returns only tenant-scoped data
- [ ] Direct database queries filtered
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] User A cannot see User B's data (different tenants)
- [ ] Global query filter active
- [ ] API returns only tenant-scoped data
- [ ] Direct database queries filtered
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

---

## Role-Based Access Control

### Coach Role Assignment

**Keycloak**
- [ ] Role assigned in database (UserRole record)
- [ ] JWT includes Coach role claim
- [ ] /api/auth/me returns Coach in roles array
- [ ] Can access Coach-only endpoints
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Role assigned in database (UserRole record)
- [ ] JWT includes Coach role claim
- [ ] /api/auth/me returns Coach in roles array
- [ ] Can access Coach-only endpoints
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Gymnast Role Assignment

**Keycloak**
- [ ] Role assigned in database (UserRole record)
- [ ] JWT includes Gymnast role claim
- [ ] /api/auth/me returns Gymnast in roles array
- [ ] Can access Gymnast-only endpoints
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Role assigned in database (UserRole record)
- [ ] JWT includes Gymnast role claim
- [ ] /api/auth/me returns Gymnast in roles array
- [ ] Can access Gymnast-only endpoints
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Platform Admin Role

**Keycloak**
- [ ] platform_admin role assigned in Keycloak
- [ ] JWT includes platform_admin claim
- [ ] Can access admin-only endpoints
- [ ] AdminPolicy authorization succeeds
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] platform_admin app role assigned in Entra
- [ ] JWT includes platform_admin claim
- [ ] Can access admin-only endpoints
- [ ] AdminPolicy authorization succeeds
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Unauthorized Access (No Role)

**Keycloak**
- [ ] User with no roles blocked from protected endpoints
- [ ] Returns 403 Forbidden
- [ ] Error message clear
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] User with no roles blocked from protected endpoints
- [ ] Returns 403 Forbidden
- [ ] Error message clear
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

### Unauthorized Access (Wrong Role)

**Keycloak**
- [ ] Gymnast cannot access Coach-only endpoint
- [ ] Returns 403 Forbidden
- [ ] Authorization policy enforced correctly
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

**Entra ID**
- [ ] Gymnast cannot access Coach-only endpoint
- [ ] Returns 403 Forbidden
- [ ] Authorization policy enforced correctly
- **Result:** ⬜ Pass / ❌ Fail / ⏭️ Skipped
- **Notes:**

---

## Performance Metrics

### Authentication Latency

| Metric | Keycloak | Entra ID | Target | Pass? |
|--------|----------|----------|--------|-------|
| P50 Login Time | ___ ms | ___ ms | < 1000ms | ⬜ / ❌ |
| P95 Login Time | ___ ms | ___ ms | < 2000ms | ⬜ / ❌ |
| P99 Login Time | ___ ms | ___ ms | < 5000ms | ⬜ / ❌ |
| P50 Token Refresh | ___ ms | ___ ms | < 500ms | ⬜ / ❌ |
| P95 Token Refresh | ___ ms | ___ ms | < 1000ms | ⬜ / ❌ |

### Throughput

| Metric | Keycloak | Entra ID | Target | Pass? |
|--------|----------|----------|--------|-------|
| Concurrent Users (50) | ⬜ Pass / ❌ Fail | ⬜ Pass / ❌ Fail | All succeed | ⬜ / ❌ |
| Concurrent Users (100) | ⬜ Pass / ❌ Fail | ⬜ Pass / ❌ Fail | All succeed | ⬜ / ❌ |
| API RPS (/api/auth/me) | ___ req/s | ___ req/s | > 100 req/s | ⬜ / ❌ |

### Failure Rates

| Metric | Keycloak | Entra ID | Target | Pass? |
|--------|----------|----------|--------|-------|
| Authentication Failure Rate | ___% | ___% | < 5% | ⬜ / ❌ |
| Token Refresh Failure Rate | ___% | ___% | < 1% | ⬜ / ❌ |
| API Call Failure Rate (401/403) | ___% | ___% | < 2% | ⬜ / ❌ |

---

## Issues Found

### Critical Issues (P0)

#### Keycloak
1. **Issue:** [Description]
   - **Impact:** [High/Medium/Low]
   - **Steps to Reproduce:**
   - **Expected Behavior:**
   - **Actual Behavior:**
   - **Status:** 🔴 Open / 🟡 In Progress / 🟢 Fixed

#### Entra ID
1. **Issue:** [Description]
   - **Impact:** [High/Medium/Low]
   - **Steps to Reproduce:**
   - **Expected Behavior:**
   - **Actual Behavior:**
   - **Status:** 🔴 Open / 🟡 In Progress / 🟢 Fixed

### Major Issues (P1)

#### Keycloak
1. **Issue:** [Description]
   - **Impact:** [High/Medium/Low]
   - **Workaround:** [If available]
   - **Status:** 🔴 Open / 🟡 In Progress / 🟢 Fixed

#### Entra ID
1. **Issue:** [Description]
   - **Impact:** [High/Medium/Low]
   - **Workaround:** [If available]
   - **Status:** 🔴 Open / 🟡 In Progress / 🟢 Fixed

### Parity Issues

1. **Feature:** [Name]
   - **Works in Keycloak:** ⬜ Yes / ❌ No
   - **Works in Entra ID:** ⬜ Yes / ❌ No
   - **Description:**
   - **Blocker for Migration:** ⬜ Yes / ❌ No

---

## Recommendations

- [ ] **Proceed to Phase 6 (Production Migration)**
  _All P0 scenarios passing, no critical issues_

- [ ] **Fix critical issues before migration**
  _Issue IDs: [List]_

- [ ] **Additional testing required**
  _Areas: [List specific areas]_

- [ ] **Performance optimization needed**
  _Metrics below target: [List]_

- [ ] **Documentation updates needed**
  _Areas: [List]_

---

## Sign-Off

### Quality Gates

- [ ] All P0 scenarios passing (100%)
- [ ] Performance metrics within targets
- [ ] No data loss observed
- [ ] No security vulnerabilities found
- [ ] No critical bugs open
- [ ] Parity achieved between providers (or documented exceptions)

### Approvals

**QA Lead:** _________________________
**Date:** _________________________

**Security Lead:** _________________________
**Date:** _________________________

**Platform Architect:** _________________________
**Date:** _________________________

**Product Owner:** _________________________
**Date:** _________________________

---

**Final Decision:**

- [ ] ✅ **APPROVED** - Proceed to Production Migration (Phase 6)
- [ ] 🟡 **APPROVED WITH CONDITIONS** - Fix issues: [List]
- [ ] ❌ **REJECTED** - Critical issues must be resolved before migration

**Notes:**

[Additional comments, concerns, or observations]
