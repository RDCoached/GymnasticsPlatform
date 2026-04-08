# Phase 7: Cleanup & Keycloak Decommission Runbook

## Overview

This runbook provides step-by-step procedures for safely removing Keycloak code, dependencies, and infrastructure after successful migration to Microsoft Entra ID.

**Prerequisites:**
- Phase 6 migration completed successfully
- 7+ days of stable operation on Entra ID
- No open Keycloak-related issues
- All quality gates met
- Stakeholder approval obtained

**Timeline:** 1-2 weeks
**Risk Level:** Low (reversible via git revert if needed)

---

## Pre-Cleanup Checklist

### Validation Criteria

**Migration Success Confirmation:**
- [ ] Migration completed 7+ days ago
- [ ] Authentication success rate stable at >95%
- [ ] Zero critical incidents in past 7 days
- [ ] Support ticket volume normalized (<10/day)
- [ ] All users migrated to Entra ID (no Keycloak logins in 7 days)
- [ ] User feedback predominantly positive
- [ ] Platform architect approval obtained
- [ ] Product owner approval obtained

### Data Preservation

**Before Cleanup:**
- [ ] Keycloak realm exported (JSON backup)
- [ ] Database backup taken (includes all user mappings)
- [ ] Configuration files backed up
- [ ] Keycloak logs archived (if needed for compliance)
- [ ] Audit trail complete in database

**Verification Query:**
```sql
-- Confirm no Keycloak users logged in recently
SELECT COUNT(*)
FROM "AuditLogs"
WHERE "Action" = 'UserAuthenticated'
  AND "NewValue" LIKE '%Keycloak%'
  AND "PerformedAt" > NOW() - INTERVAL '7 days';

-- Expected: 0
```

---

## Cleanup Phases

### Phase 7A: Backend Cleanup (Code Removal)

**Duration:** 2-3 hours
**Owner:** Development Team

#### Step 1: Remove Keycloak Authentication Provider

**Files to Delete:**

```bash
# Remove Keycloak adapter
rm src/Modules/Auth/Auth.Infrastructure/Services/KeycloakAuthenticationProvider.cs

# Remove Keycloak admin service interface
rm src/Modules/Auth/Auth.Application/Services/IKeycloakAdminService.cs

# Remove Keycloak admin service implementation
rm src/Modules/Auth/Auth.Infrastructure/Services/KeycloakAdminService.cs
```

**Verification:**
```bash
# Search for remaining Keycloak references
grep -r "Keycloak" src/ --exclude-dir=bin --exclude-dir=obj
# Expected: No results (except comments)

# Search for IKeycloakAdminService usage
grep -r "IKeycloakAdminService" src/ --exclude-dir=bin --exclude-dir=obj
# Expected: No results
```

#### Step 2: Update Dependency Injection

**File:** `src/GymnasticsPlatform.Api/Program.cs`

**Remove:**
```csharp
// Old: Provider selection logic
var activeProvider = builder.Configuration["Authentication:ActiveProvider"] ?? "Keycloak";

if (activeProvider.Equals("EntraId", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<Auth.Application.Services.IAuthenticationProvider,
        Auth.Infrastructure.Services.EntraIdAuthenticationProvider>();
}
else
{
    builder.Services.AddScoped<Auth.Application.Services.IAuthenticationProvider,
        Auth.Infrastructure.Services.KeycloakAuthenticationProvider>();
}
```

**Replace with:**
```csharp
// Entra ID is now the only provider
builder.Services.AddScoped<Auth.Application.Services.IAuthenticationProvider,
    Auth.Infrastructure.Services.EntraIdAuthenticationProvider>();
```

**Also Remove Keycloak JWT Configuration:**
```csharp
// Remove this entire block:
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (activeProvider.Equals("EntraId", StringComparison.OrdinalIgnoreCase))
        {
            // Entra config...
        }
        else
        {
            // REMOVE THIS BRANCH:
            var keycloakConfig = builder.Configuration.GetSection("Authentication:Keycloak");
            options.Authority = keycloakConfig["Authority"];
            options.Audience = keycloakConfig["Audience"];
        }
    });
```

**Replace with:**
```csharp
// Entra ID JWT configuration only
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var entraConfig = builder.Configuration.GetSection("Authentication:EntraId");
        options.Authority = $"{entraConfig["Instance"]}{entraConfig["TenantId"]}/v2.0";
        options.Audience = entraConfig["Audience"] ?? "api://gymnastics-api";
    });
```

#### Step 3: Remove Keycloak Configuration

**File:** `src/GymnasticsPlatform.Api/appsettings.json`

**Remove:**
```json
{
  "Authentication": {
    "ActiveProvider": "EntraId",  // DELETE THIS LINE
    "Keycloak": {                 // DELETE THIS ENTIRE SECTION
      "Authority": "...",
      "Audience": "...",
      "AdminApiUrl": "...",
      "Realm": "...",
      "ClientId": "...",
      "ClientSecret": "..."
    },
    "EntraId": {
      // Keep this section
    }
  }
}
```

**Result:**
```json
{
  "Authentication": {
    "EntraId": {
      "TenantId": "...",
      "ApiClientId": "...",
      "ApiClientSecret": "...",
      "Instance": "https://login.microsoftonline.com/",
      "Audience": "api://gymnastics-api",
      "ExtensionAppId": "...",
      "TenantIdExtensionAttributeName": "..."
    }
  }
}
```

#### Step 4: Remove Keycloak NuGet Packages

**Check installed Keycloak packages:**
```bash
cd src/Modules/Auth/Auth.Infrastructure
dotnet list package | grep -i keycloak
```

**If any Keycloak packages exist, remove them:**
```bash
# Example (adjust package names as needed)
dotnet remove package Keycloak.AuthServices.Authentication
dotnet remove package Keycloak.AuthServices.Authorization
```

#### Step 5: Remove HTTP Client Factory for Keycloak

**File:** `src/GymnasticsPlatform.Api/Program.cs`

**Remove:**
```csharp
// Remove Keycloak HTTP client registration
builder.Services.AddHttpClient("KeycloakAdmin", client =>
{
    var config = builder.Configuration.GetSection("Authentication:Keycloak");
    client.BaseAddress = new Uri(config["AdminApiUrl"]!);
});
```

#### Step 6: Build and Test

**Build solution:**
```bash
dotnet build
# Expected: Build succeeded, 0 errors
```

**Run tests:**
```bash
dotnet test
# Expected: All tests pass
```

**Check for compilation errors:**
```bash
# If any errors, they should be about missing Keycloak references
# Remove those references
```

---

### Phase 7B: Frontend Cleanup (Dependency Removal)

**Duration:** 1-2 hours
**Owner:** Frontend Team

#### Step 1: Remove Keycloak Packages

**Check installed packages:**
```bash
cd frontend/user-portal
npm list | grep keycloak
```

**Remove Keycloak dependencies:**
```bash
npm uninstall @react-keycloak/web keycloak-js
```

**Verify `package.json`:**
```json
{
  "dependencies": {
    // These should be REMOVED:
    // "@react-keycloak/web": "^3.4.0",
    // "keycloak-js": "^26.0.0",

    // These should REMAIN:
    "@azure/msal-browser": "^3.31.0",
    "@azure/msal-react": "^2.1.3",
    // ... other dependencies
  }
}
```

#### Step 2: Remove Keycloak Provider Files

**Files to delete:**
```bash
cd frontend/user-portal/src

# Remove Keycloak provider
rm providers/KeycloakAuthProvider.tsx

# Remove Keycloak config
rm keycloak.ts

# Remove Keycloak environment template (if exists)
rm ../env.keycloak
```

#### Step 3: Update AuthProvider

**File:** `frontend/user-portal/src/providers/AuthProvider.tsx`

**Remove conditional provider selection:**

**Before:**
```typescript
export function AuthProvider({ children }: AuthProviderProps) {
  const authProvider = import.meta.env.VITE_AUTH_PROVIDER?.toLowerCase() || 'keycloak';

  if (authProvider === 'entra') {
    return <EntraAuthProvider>{children}</EntraAuthProvider>;
  }

  return <KeycloakAuthProvider>{children}</KeycloakAuthProvider>;
}
```

**After:**
```typescript
export function AuthProvider({ children }: AuthProviderProps) {
  // Entra ID is now the only authentication provider
  return <EntraAuthProvider>{children}</EntraAuthProvider>;
}
```

#### Step 4: Clean Up Environment Variables

**File:** `frontend/user-portal/.env.example`

**Remove Keycloak variables:**

**Before:**
```bash
# Authentication Provider
VITE_AUTH_PROVIDER=keycloak  # or 'entra'

# Keycloak Configuration
VITE_KEYCLOAK_URL=http://localhost:8080
VITE_KEYCLOAK_REALM=gymnastics
VITE_KEYCLOAK_CLIENT_ID=user-portal

# Entra ID Configuration
VITE_ENTRA_CLIENT_ID=
VITE_ENTRA_TENANT_ID=
```

**After:**
```bash
# Microsoft Entra ID Configuration
VITE_ENTRA_CLIENT_ID=
VITE_ENTRA_TENANT_ID=
VITE_REDIRECT_URI=http://localhost:5173/auth/callback

# API Configuration
VITE_API_URL=http://localhost:5001
```

**Update production `.env` files:**
```bash
# Remove VITE_AUTH_PROVIDER from all .env files
# Remove all VITE_KEYCLOAK_* variables
```

#### Step 5: Build and Test

**Install dependencies (verifies no Keycloak deps):**
```bash
npm install
# Expected: No errors, no Keycloak packages installed
```

**Build:**
```bash
npm run build
# Expected: Build successful
```

**Run tests:**
```bash
npm test
# Expected: All tests pass
```

**Manual smoke test:**
```bash
npm run dev
# Open http://localhost:5173
# Verify:
# - Sign-in page loads
# - "Sign in with Microsoft" button visible
# - "Sign in with Google" button visible
# - No "Keycloak" references in UI
```

---

### Phase 7C: Infrastructure Cleanup

**Duration:** 30 minutes - 1 hour
**Owner:** DevOps Team

#### Step 1: Archive Keycloak Realm

**Export realm before decommission:**

```bash
# SSH to Keycloak server
ssh keycloak-server

# Export realm
docker exec keycloak /opt/keycloak/bin/kc.sh export \
  --realm gymnastics \
  --file /tmp/gymnastics-realm-backup-$(date +%Y%m%d).json

# Copy to backup storage
docker cp keycloak:/tmp/gymnastics-realm-backup-*.json ./
scp gymnastics-realm-backup-*.json backup-storage:/archives/keycloak/

# Verify export
ls -lh gymnastics-realm-backup-*.json
# File should be several MB

# Optional: Verify JSON is valid
cat gymnastics-realm-backup-*.json | jq . > /dev/null
# Expected: No errors
```

**Archive realm to Azure Blob Storage (if using Azure):**
```bash
az storage blob upload \
  --account-name gymnasticsbackups \
  --container-name keycloak-archives \
  --name gymnastics-realm-backup-$(date +%Y%m%d).json \
  --file gymnastics-realm-backup-$(date +%Y%m%d).json
```

#### Step 2: Remove Keycloak from Docker Compose

**File:** `docker-compose.yml`

**Remove Keycloak service:**
```yaml
services:
  # REMOVE THIS ENTIRE SERVICE:
  # keycloak:
  #   image: quay.io/keycloak/keycloak:26.0.0
  #   environment:
  #     - KEYCLOAK_ADMIN=admin
  #     - KEYCLOAK_ADMIN_PASSWORD=admin
  #     - KC_DB=postgres
  #     - KC_DB_URL=jdbc:postgresql://postgres:5432/keycloak
  #     - KC_DB_USERNAME=keycloak
  #     - KC_DB_PASSWORD=keycloak
  #   ports:
  #     - "8080:8080"
  #   depends_on:
  #     - postgres
  #   command: start-dev

  # Keep other services (PostgreSQL, Grafana, etc.)
  postgres:
    # ...
```

**Remove Keycloak database from PostgreSQL init:**

**File:** `docker/postgres/init.sql` (if exists)

**Remove:**
```sql
-- CREATE DATABASE keycloak;
-- CREATE USER keycloak WITH PASSWORD 'keycloak';
-- GRANT ALL PRIVILEGES ON DATABASE keycloak TO keycloak;
```

#### Step 3: Stop and Remove Keycloak Container

**Stop Keycloak:**
```bash
docker-compose stop keycloak
# Or if standalone:
docker stop keycloak
```

**Remove Keycloak container:**
```bash
docker-compose rm keycloak
# Or if standalone:
docker rm keycloak
```

**Remove Keycloak volumes (if any):**
```bash
docker volume ls | grep keycloak
# If volumes exist:
docker volume rm <volume-name>
```

**Verify removal:**
```bash
docker ps -a | grep keycloak
# Expected: No results
```

#### Step 4: Decommission Keycloak Server (Production)

**If using dedicated Keycloak VM/container in production:**

**Azure:**
```bash
# Stop VM
az vm stop --resource-group gymnastics-prod --name keycloak-vm

# Deallocate (stops billing)
az vm deallocate --resource-group gymnastics-prod --name keycloak-vm

# Optional: Delete VM after 30-day grace period
# az vm delete --resource-group gymnastics-prod --name keycloak-vm --yes
```

**AWS:**
```bash
# Stop instance
aws ec2 stop-instances --instance-ids i-1234567890abcdef0

# Optional: Terminate after 30-day grace period
# aws ec2 terminate-instances --instance-ids i-1234567890abcdef0
```

**Kubernetes:**
```bash
# Scale down deployment
kubectl scale deployment keycloak --replicas=0 -n gymnastics

# Optional: Delete deployment after 30-day grace period
# kubectl delete deployment keycloak -n gymnastics
```

#### Step 5: Update DNS / Load Balancer

**Remove Keycloak DNS entry:**
```bash
# Azure DNS
az network dns record-set a delete \
  --resource-group gymnastics-prod \
  --zone-name gymnastics.example.com \
  --name keycloak \
  --yes

# Or update your DNS provider to remove keycloak.gymnastics.example.com
```

**Remove load balancer backend (if applicable):**
```bash
# Azure Load Balancer
az network lb rule delete \
  --resource-group gymnastics-prod \
  --lb-name gymnastics-lb \
  --name keycloak-rule
```

#### Step 6: Clean Up Keycloak Database

**Optional: Drop Keycloak database (after 30-day grace period):**

```sql
-- Connect to PostgreSQL
psql -h prod-db.postgres.azure.com -U admin -d postgres

-- Drop Keycloak database
DROP DATABASE IF EXISTS keycloak;
DROP USER IF EXISTS keycloak;
```

**Note:** Only do this after verifying no rollback will be needed (30+ days post-migration).

---

### Phase 7D: Documentation Updates

**Duration:** 2-3 hours
**Owner:** Documentation Team

#### Step 1: Update README.md

**File:** `/README.md`

**Remove Keycloak references:**

**Before:**
```markdown
## Authentication

The platform uses Keycloak for authentication with support for:
- Email/password login
- Google OAuth
```

**After:**
```markdown
## Authentication

The platform uses Microsoft Entra ID for authentication with support for:
- Email/password login
- Google OAuth
- Microsoft OAuth
```

**Update setup instructions:**

**Before:**
```markdown
### Prerequisites
- Docker Desktop
- .NET 10 SDK
- Node.js 20+
- Keycloak 26 (via Docker)
```

**After:**
```markdown
### Prerequisites
- Docker Desktop
- .NET 10 SDK
- Node.js 20+
- Microsoft Entra ID tenant (see ENTRA_ID_SETUP.md)
```

**Update quick start:**

**Before:**
```markdown
### Quick Start
1. Start infrastructure: `docker-compose up -d` (includes Keycloak)
2. Navigate to Keycloak: http://localhost:8080
3. Import realm: `docs/keycloak-realm-export.json`
```

**After:**
```markdown
### Quick Start
1. Set up Entra ID (one-time): Follow `docs/ENTRA_ID_SETUP.md`
2. Configure backend: Update `appsettings.json` with Entra ID values
3. Configure frontend: Update `.env` with Entra ID client ID
4. Start infrastructure: `docker-compose up -d`
```

#### Step 2: Archive Keycloak Documentation

**Move (don't delete) Keycloak docs:**
```bash
mkdir docs/archive
mv docs/KEYCLOAK_SETUP.md docs/archive/
echo "This document is archived. Keycloak was replaced by Microsoft Entra ID in 2026." > docs/archive/README.md
```

#### Step 3: Update CLAUDE.md (Project Instructions)

**File:** `/CLAUDE.md`

**Update authentication references:**

**Before:**
```markdown
## Authentication
- Keycloak 26 (Google OAuth + JWT + email/password)
```

**After:**
```markdown
## Authentication
- Microsoft Entra ID (Google OAuth + Microsoft OAuth + JWT + email/password)
```

#### Step 4: Update Frontend README

**File:** `/frontend/README.md`

**Update authentication flow description:**

**Before:**
```markdown
### Authentication Flow
1. User clicks "Sign in with Google"
2. Redirected to Keycloak
3. Keycloak federates to Google
4. User approves Google OAuth
5. Redirected back to Keycloak
6. Keycloak issues JWT
7. Frontend receives token
```

**After:**
```markdown
### Authentication Flow
1. User clicks "Sign in with Google/Microsoft"
2. MSAL popup opens with Entra ID login
3. Entra federates to Google (or native Microsoft)
4. User approves OAuth
5. Entra issues JWT with tenant_id claim
6. MSAL caches token in sessionStorage
7. Frontend uses token for API calls
```

#### Step 5: Update API Documentation

**If you have API documentation (Swagger/Scalar):**

Update authentication scheme examples:

**Before:**
```json
{
  "security": [
    {
      "Keycloak": ["openid", "profile"]
    }
  ]
}
```

**After:**
```json
{
  "security": [
    {
      "EntraId": ["api://gymnastics-api/user.access"]
    }
  ]
}
```

---

### Phase 7E: Final Verification

**Duration:** 1 hour
**Owner:** QA Team

#### Step 1: Code Verification

**Search for Keycloak references:**
```bash
# Backend
grep -ri "keycloak" src/ docs/ --exclude-dir=bin --exclude-dir=obj --exclude-dir=archive
# Expected: Only in archived docs or comments

# Frontend
grep -ri "keycloak" frontend/ --exclude-dir=node_modules --exclude-dir=dist
# Expected: No results

# Infrastructure
grep -ri "keycloak" docker-compose.yml
# Expected: No results
```

#### Step 2: Build Verification

**Backend:**
```bash
dotnet clean
dotnet build
# Expected: Build succeeded, 0 warnings, 0 errors
```

**Frontend:**
```bash
cd frontend/user-portal
rm -rf node_modules package-lock.json
npm install
npm run build
# Expected: Build successful, no Keycloak dependencies
```

#### Step 3: Test Suite Verification

**Run all tests:**
```bash
dotnet test
# Expected: All tests pass
# Note: Any Keycloak-specific tests should have been removed
```

**Check test files for Keycloak references:**
```bash
grep -r "Keycloak" tests/ --exclude-dir=bin --exclude-dir=obj
# Expected: No results
```

#### Step 4: Runtime Verification

**Start application:**
```bash
docker-compose up -d
dotnet run --project src/GymnasticsPlatform.Api
cd frontend/user-portal && npm run dev
```

**Manual smoke test:**
1. **Registration:**
   - Navigate to sign-up page
   - Register new account with email/password
   - Verify email sent
   - Verify account created in Entra ID (Azure Portal)

2. **Login:**
   - Email/password login ✓
   - Google OAuth login ✓
   - Microsoft OAuth login ✓

3. **Session Management:**
   - Token refresh works ✓
   - Logout clears session ✓

4. **API Calls:**
   - Authenticated API calls succeed ✓
   - Unauthorized calls return 401 ✓

#### Step 5: Monitoring Verification

**Check Application Insights:**
- No Keycloak provider in authentication metrics
- Only "EntraId" provider in logs
- No errors related to Keycloak

**Query:**
```kusto
traces
| where timestamp > ago(1h)
| where message contains "Keycloak" or message contains "keycloak"
| count
```
**Expected:** 0 results

---

## Post-Cleanup Checklist

### Code Cleanup

- [ ] `KeycloakAuthenticationProvider.cs` deleted
- [ ] `IKeycloakAdminService.cs` deleted
- [ ] `KeycloakAdminService.cs` deleted
- [ ] `KeycloakAuthProvider.tsx` deleted (frontend)
- [ ] `keycloak.ts` deleted (frontend)
- [ ] Keycloak NuGet packages removed
- [ ] Keycloak npm packages removed
- [ ] Provider selection logic removed from `Program.cs`
- [ ] Keycloak JWT configuration removed
- [ ] Keycloak HTTP client factory registration removed

### Configuration Cleanup

- [ ] `ActiveProvider` setting removed from `appsettings.json`
- [ ] Keycloak section removed from `appsettings.json`
- [ ] `VITE_AUTH_PROVIDER` removed from `.env`
- [ ] `VITE_KEYCLOAK_*` variables removed from `.env`
- [ ] Environment-specific configs updated (staging, production)

### Infrastructure Cleanup

- [ ] Keycloak realm exported and archived
- [ ] Keycloak container stopped and removed
- [ ] Keycloak service removed from `docker-compose.yml`
- [ ] Keycloak VM/server deallocated (production)
- [ ] Keycloak DNS entry removed
- [ ] Keycloak load balancer rules removed
- [ ] Keycloak database marked for deletion (30-day grace period)

### Documentation Cleanup

- [ ] README.md updated (authentication section)
- [ ] KEYCLOAK_SETUP.md moved to `docs/archive/`
- [ ] CLAUDE.md updated (Stack section)
- [ ] Frontend README updated (authentication flow)
- [ ] API documentation updated (security schemes)
- [ ] ENTRA_ID_SETUP.md is primary auth setup guide

### Verification

- [ ] No "Keycloak" references in code (except archived docs)
- [ ] Backend builds successfully
- [ ] Frontend builds successfully
- [ ] All tests pass
- [ ] Application runs without errors
- [ ] Authentication works (email, Google, Microsoft)
- [ ] Monitoring shows only Entra ID provider
- [ ] No Keycloak-related errors in logs

### Stakeholder Sign-Off

- [ ] Platform architect approval
- [ ] DevOps team approval
- [ ] QA team approval
- [ ] Product owner approval

---

## Rollback Plan (If Needed)

**If critical issues discovered after cleanup:**

### Immediate Rollback (Code Only)

1. **Revert code changes:**
   ```bash
   git revert <cleanup-commit-sha>
   git push origin main
   ```

2. **Redeploy:**
   ```bash
   # Backend
   dotnet build
   dotnet publish
   # Deploy to production

   # Frontend
   npm install
   npm run build
   # Deploy to CDN/static hosting
   ```

3. **Restore configuration:**
   - Re-add Keycloak section to `appsettings.json`
   - Set `ActiveProvider` back to "Keycloak"

### Full Rollback (Infrastructure Too)

**If Keycloak infrastructure is needed:**

1. **Restore Keycloak container:**
   ```bash
   # If VM was deallocated (not deleted)
   az vm start --resource-group gymnastics-prod --name keycloak-vm

   # If docker-compose
   git checkout <commit-before-cleanup> -- docker-compose.yml
   docker-compose up -d keycloak
   ```

2. **Import realm:**
   ```bash
   docker cp gymnastics-realm-backup-*.json keycloak:/tmp/
   docker exec keycloak /opt/keycloak/bin/kc.sh import \
     --file /tmp/gymnastics-realm-backup-*.json
   ```

3. **Restore DNS:**
   ```bash
   az network dns record-set a add-record \
     --resource-group gymnastics-prod \
     --zone-name gymnastics.example.com \
     --record-set-name keycloak \
     --ipv4-address <keycloak-ip>
   ```

4. **Switch provider:**
   - Update `appsettings.json`: `ActiveProvider` = "Keycloak"
   - Restart API servers

**Rollback Timeline:** 30-60 minutes

---

## Timeline

### Week 1: Preparation
- **Day 1-2:** Verify migration success (auth metrics, user feedback)
- **Day 3:** Export and archive Keycloak realm
- **Day 4:** Create cleanup PR
- **Day 5:** Code review and approval

### Week 2: Execution
- **Day 1 (Monday):** Merge cleanup PR
- **Day 1-2:** Monitor for issues
- **Day 3-4:** Decommission infrastructure (after 48-hour soak period)
- **Day 5 (Friday):** Final verification and sign-off

### Week 3: Grace Period
- **Days 1-30:** Keep Keycloak backups accessible
- **Day 30:** Permanent deletion of Keycloak database (if no issues)

---

## Cost Savings

**Monthly savings from Keycloak decommission:**

- **VM/Compute:** $50-200/month (depending on instance size)
- **Storage:** $10-20/month (database, logs)
- **Bandwidth:** $5-10/month
- **DevOps Time:** ~2 hours/month maintenance (reduced to 0)

**Total Estimated Savings:** $65-230/month ($780-2760/year)

**One-time Cleanup Cost:** ~10-15 developer hours

**ROI:** Positive after 1-2 months

---

## Final Notes

### What Gets Deleted

- Keycloak Docker container/VM
- Keycloak database (after 30 days)
- Keycloak configuration files
- Keycloak adapter code
- Keycloak npm dependencies

### What Gets Preserved

- Keycloak realm export (archived)
- Database backup (full snapshot)
- Git history (all code retrievable)
- User mappings in database (`provider_user_id`)
- Audit logs (authentication history)

### What Stays

- All user accounts (migrated to Entra ID)
- All user data (gymnasts, sessions, programs)
- All authentication flows (now via Entra ID)
- OAuth support (Google + Microsoft)

---

**Document Version:** 1.0
**Last Updated:** 2026-04-08
**Maintained By:** Platform Team
