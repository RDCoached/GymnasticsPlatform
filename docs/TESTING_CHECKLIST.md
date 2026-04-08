# Quick Testing Checklist

Use this as a quick reference during dual-provider testing. For detailed procedures, see [DUAL_PROVIDER_TESTING.md](./DUAL_PROVIDER_TESTING.md).

## Pre-Test Setup

### Backend
- [ ] `appsettings.json` configured for target provider
- [ ] Database migrations applied
- [ ] Test users created
- [ ] API server running
- [ ] Logs visible (Seq/console)

### Frontend
- [ ] `.env` configured for target provider
- [ ] `npm install` completed
- [ ] Dev server running
- [ ] Browser DevTools ready

### Tools
- [ ] k6 or load testing tool ready
- [ ] Postman/curl for API testing
- [ ] Database client connected

---

## Core Flows (Quick Check)

### Authentication
- [ ] Email/password signup
- [ ] Email/password login
- [ ] Google OAuth login
- [ ] Microsoft OAuth login (Entra only)
- [ ] Logout (session cleared)
- [ ] Token refresh (silent)

### User Management
- [ ] Get current user (`/api/auth/me`)
- [ ] Email verification
- [ ] Password reset
- [ ] Duplicate email blocked (409)

### Multi-Tenancy
- [ ] New user → onboarding tenant
- [ ] `tenant_id` in JWT
- [ ] Tenant update after onboarding
- [ ] Data isolation (User A ≠ User B)

### Authorization
- [ ] Coach role assigned
- [ ] Gymnast role assigned
- [ ] Admin access (platform_admin)
- [ ] Unauthorized blocked (403)

---

## Performance Quick Check

### Latency Targets
- [ ] P95 login < 2s
- [ ] P95 token refresh < 1s
- [ ] API call < 500ms

### Load Test
- [ ] 50 concurrent users succeed
- [ ] 100 concurrent users succeed

### Failure Rates
- [ ] Auth failures < 5%
- [ ] Token refresh failures < 1%

---

## Provider Switch Checklist

### Keycloak → Entra ID

**Backend**
1. [ ] Stop API
2. [ ] `ActiveProvider` = "EntraId" in `appsettings.json`
3. [ ] Verify Entra config complete
4. [ ] Start API
5. [ ] Check logs: "Active authentication provider: EntraId"

**Frontend**
1. [ ] Stop dev server
2. [ ] `cp .env.entra .env`
3. [ ] `npm run build`
4. [ ] `npm run dev`
5. [ ] Verify `VITE_AUTH_PROVIDER=entra` in console

### Entra ID → Keycloak

**Backend**
1. [ ] Stop API
2. [ ] `ActiveProvider` = "Keycloak" in `appsettings.json`
3. [ ] Verify Keycloak config complete
4. [ ] Start API
5. [ ] Check logs: "Active authentication provider: Keycloak"

**Frontend**
1. [ ] Stop dev server
2. [ ] `cp .env.keycloak .env`
3. [ ] `npm run build`
4. [ ] `npm run dev`
5. [ ] Verify `VITE_AUTH_PROVIDER=keycloak` in console

---

## Common Issues (Quick Reference)

| Symptom | Likely Cause | Quick Fix |
|---------|--------------|-----------|
| 401 Unauthorized | Token invalid/expired | Refresh token or re-login |
| 403 Forbidden | Missing role | Assign role in database |
| CORS error | Origin not allowed | Add to CORS policy in Program.cs |
| "Invalid redirect URI" | URI not registered | Add to app registration |
| Session cookie not set | Missing `credentials: 'include'` | Update fetch calls |
| `tenant_id` missing in JWT | Optional claims not configured | Add to token configuration |
| Extension attribute not found | Not created in Entra | Run PowerShell script from setup guide |

---

## Quick Validation Commands

### Check Active Provider (Backend)
```bash
# View current config
cat appsettings.json | grep ActiveProvider

# Or via API
curl http://localhost:5001/api/health | jq
```

### Check Active Provider (Frontend)
```bash
# View .env
cat frontend/user-portal/.env | grep VITE_AUTH_PROVIDER

# Or in browser console
console.log(import.meta.env.VITE_AUTH_PROVIDER)
```

### Test API Authentication
```bash
# Get token
TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}' \
  | jq -r '.accessToken')

# Call protected endpoint
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5001/api/auth/me | jq
```

### Check Database Tenant
```sql
-- View user's current tenant
SELECT "ProviderUserId", "Email", "TenantId"
FROM "UserProfiles"
WHERE "Email" = 'test@example.com';

-- View user's roles
SELECT ur."TenantId", ur."Role", up."Email"
FROM "UserRoles" ur
JOIN "UserProfiles" up ON ur."ProviderUserId" = up."ProviderUserId"
WHERE up."Email" = 'test@example.com';
```

---

## Load Test Commands

### k6 Quick Test
```bash
# Basic load test
k6 run --vus 50 --duration 60s scripts/load-test-login.js

# With thresholds
k6 run --vus 100 --duration 120s \
  --threshold http_req_duration=p95<2000 \
  scripts/load-test-login.js
```

### Artillery Quick Test
```bash
# Ramp-up test
artillery quick --count 50 --num 10 http://localhost:5001/api/auth/me
```

---

## Decision Matrix

### Proceed to Phase 6?

| Criteria | Status | Required? |
|----------|--------|-----------|
| All P0 tests pass | ⬜ / ❌ | ✅ Yes |
| P95 login < 2s | ⬜ / ❌ | ✅ Yes |
| Auth failures < 5% | ⬜ / ❌ | ✅ Yes |
| No critical bugs | ⬜ / ❌ | ✅ Yes |
| No data loss | ⬜ / ❌ | ✅ Yes |
| Parity achieved | ⬜ / ❌ | ⚠️ Nice to have |
| All P1 tests pass | ⬜ / ❌ | ⚠️ Nice to have |

**Go/No-Go Decision:**

- ✅ **GO** - All required criteria met
- 🟡 **GO WITH CONDITIONS** - Fix issues then proceed
- ❌ **NO-GO** - Critical issues block migration
