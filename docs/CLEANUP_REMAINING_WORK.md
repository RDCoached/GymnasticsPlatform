# Phase 7 Cleanup - Remaining Work

## Overview

Phase 7 cleanup has been partially completed. This document tracks the remaining manual cleanup tasks.

## Completed ✅

### Backend
- [x] Removed `KeycloakAuthenticationProvider.cs`
- [x] Removed `IKeycloakAdminService.cs`
- [x] Removed `KeycloakAdminService.cs`
- [x] Removed `KeycloakSettings.cs`
- [x] Updated `Program.cs` (removed provider selection, Keycloak HTTP client, Keycloak JWT config)
- [x] Updated `appsettings.json` (removed Keycloak configuration sections)
- [x] Updated `UserTenantService.cs` (now uses `IAuthenticationProvider`)

### Frontend
- [x] Removed `KeycloakAuthProvider.tsx`
- [x] Removed `keycloak.ts` (user portal and admin portal)
- [x] Updated `AuthProvider.tsx` (now uses `EntraAuthProvider`)
- [x] Updated `.env.example` (removed Keycloak variables, added Entra ID variables)
- [x] Removed `@react-keycloak/web` npm package
- [x] Removed `keycloak-js` npm package

### Tests
- [x] Removed `KeycloakAdminServiceTests.cs`

---

## Remaining Work ⚠️

### 1. SessionAuthMiddleware.cs

**File:** `/src/GymnasticsPlatform.Api/Middleware/SessionAuthMiddleware.cs`

**Issue:** References `KeycloakSettings` in 3 places

**Investigation Needed:**
- Determine if this middleware is still needed with Entra ID
- If needed: Update to work without Keycloak-specific settings
- If not needed: Remove middleware and its registration in Program.cs

**Lines with Keycloak references:**
```csharp
// Constructor parameter
IOptions<KeycloakSettings> keycloakSettings,

// Field assignment
private readonly KeycloakSettings _keycloakSettings = keycloakSettings.Value;

// RefreshAccessTokenAsync method
KeycloakSettings settings,
```

**Suggested Approach:**

**Option A: Remove SessionAuthMiddleware** (if session auth not needed)
- Remove middleware file
- Remove `app.UseMiddleware<SessionAuthMiddleware>()` from Program.cs (line 328)
- Verify application works with JWT-only authentication

**Option B: Update SessionAuthMiddleware** (if session auth still needed)
- Remove Keycloak-specific token refresh logic
- Update to use Entra ID token endpoint for refresh
- Update configuration to use Entra settings instead of Keycloak settings

**Recommendation:** Option A (Remove) - Entra ID with MSAL handles token refresh client-side. Server-side session middleware may not be necessary.

---

### 2. AuthEndpoints.cs

**File:** `/src/GymnasticsPlatform.Api/Endpoints/AuthEndpoints.cs`

**Issue:** 4 endpoint methods still reference `IKeycloakAdminService` as a parameter

**Lines with IKeycloakAdminService references:**
1. Line 57: `Register` endpoint
2. Line 140: `Login` endpoint
3. Line 244: `InitiatePasswordReset` endpoint
4. Line 287: `ResetPassword` endpoint

**Fix Required:**

Replace all `IKeycloakAdminService keycloakService` parameters with `IAuthenticationProvider authProvider`.

**Example:**

**Before:**
```csharp
private static async Task<IResult> Register(
    RegisterRequest request,
    IKeycloakAdminService keycloakService,  // OLD
    IUserTenantService userTenantService,
    IValidator<RegisterRequest> validator,
    HttpContext context,
    CancellationToken ct)
{
    // ...
    var createUserResult = await keycloakService.CreateUserAsync(...);  // OLD
}
```

**After:**
```csharp
private static async Task<IResult> Register(
    RegisterRequest request,
    IAuthenticationProvider authProvider,  // NEW
    IUserTenantService userTenantService,
    IValidator<RegisterRequest> validator,
    HttpContext context,
    CancellationToken ct)
{
    // ...
    var createUserResult = await authProvider.CreateUserAsync(...);  // NEW
}
```

**Apply this change to all 4 methods:**
- `Register` (line ~57)
- `Login` (line ~140)
- `InitiatePasswordReset` (line ~244)
- `ResetPassword` (line ~287)

---

## Testing After Completion

### Build Verification
```bash
dotnet clean
dotnet build
# Expected: Build succeeded, 0 errors
```

### Test Suite
```bash
dotnet test
# Expected: All tests pass
```

### Search for Remaining References
```bash
# Backend
grep -r "Keycloak\|keycloak" src/ --include="*.cs" | grep -v "//.*Keycloak" | grep -v archive
# Expected: No results (except comments)

# Frontend
grep -r "Keycloak\|keycloak" frontend/ --include="*.ts" --include="*.tsx" | grep -v node_modules
# Expected: No results
```

### Runtime Verification
```bash
# Start API
dotnet run --project src/GymnasticsPlatform.Api

# Check logs
# Expected: "Active authentication provider: EntraId" (if logging exists)
# Expected: No Keycloak-related errors

# Test authentication
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","fullName":"Test User"}'
# Expected: 201 Created (or appropriate response)
```

---

## Completion Checklist

Once remaining work is done:

- [ ] SessionAuthMiddleware handled (removed or updated)
- [ ] AuthEndpoints updated (all 4 methods)
- [ ] Build succeeds with zero errors
- [ ] All tests pass
- [ ] No Keycloak references remain in codebase (except archived docs/comments)
- [ ] Application runs without errors
- [ ] Authentication flows work (register, login, OAuth)
- [ ] Commit changes
- [ ] Update Phase 7 PR or create follow-up PR

---

## Estimated Time

- **SessionAuthMiddleware investigation & fix:** 30-60 minutes
- **AuthEndpoints updates (4 methods):** 15-30 minutes
- **Testing & verification:** 15-30 minutes
- **Total:** 1-2 hours

---

**Document Version:** 1.0
**Created:** 2026-04-08
**Status:** In Progress
