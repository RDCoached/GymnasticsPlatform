# Production Migration Runbook - Big Bang Approach

## Overview

This runbook provides step-by-step procedures for migrating from Keycloak to Microsoft Entra ID in production using the **big bang** approach. All users will switch to Entra ID simultaneously during a scheduled maintenance window.

**Migration Strategy:** Big Bang (Single Cutover)
**Estimated Duration:** 2-3 hours
**Recommended Day/Time:** Saturday 6:00 AM UTC (low traffic period)
**Prerequisites:** Phase 5 testing completed with all quality gates passed

---

## Pre-Migration Checklist (1 Week Before)

### Phase 5 Sign-Off

- [ ] All P0 test scenarios passing (100%)
- [ ] All P1 test scenarios passing (>95%)
- [ ] Performance metrics within targets
- [ ] No critical bugs open
- [ ] QA sign-off obtained
- [ ] Security sign-off obtained
- [ ] Platform architect sign-off obtained
- [ ] Product owner sign-off obtained

### Infrastructure

- [ ] Production Entra ID tenant configured (4 app registrations)
- [ ] Extension attribute created and verified
- [ ] Google external identity provider configured
- [ ] Production API app has client secret in Azure Key Vault
- [ ] SPA redirect URIs registered for production URLs
- [ ] CORS origins configured for production
- [ ] SSL certificates valid for all domains

### Configuration

- [ ] Production `appsettings.json` updated with Entra ID config (but `ActiveProvider` still = "Keycloak")
- [ ] Frontend `.env.production` created with Entra ID values
- [ ] Feature flag ready to flip: `ActiveProvider` → "EntraId"
- [ ] Database migration applied to staging (verified)
- [ ] Configuration values verified with `validate-config.sh`

### Database

- [ ] Full database backup taken
- [ ] Backup verified (restore test successful)
- [ ] `provider_user_id` column exists (VARCHAR(255))
- [ ] All existing users have `ProviderUserId` populated
- [ ] Test restore to staging environment successful

### Monitoring & Observability

- [ ] Application Insights configured for production
- [ ] Log retention set to 90 days
- [ ] Alert rules configured:
  - Authentication failure rate > 10%
  - API error rate > 5%
  - Database connection failures
  - CPU/Memory high usage
- [ ] Monitoring dashboard created (see [MONITORING_DASHBOARD.md](./MONITORING_DASHBOARD.md))
- [ ] On-call rotation defined for migration day

### Communication

- [ ] User communication drafted and approved (see [MIGRATION_USER_COMMS.md](./MIGRATION_USER_COMMS.md))
- [ ] Maintenance window scheduled in calendar
- [ ] Announcement sent 7 days before (T-7)
- [ ] Reminder sent 3 days before (T-3)
- [ ] Reminder sent 1 day before (T-1)
- [ ] Status page updated with maintenance notice

### Team Readiness

- [ ] DevOps team briefed on runbook
- [ ] Support team trained on Entra ID troubleshooting (see [SUPPORT_GUIDE.md](./SUPPORT_GUIDE.md))
- [ ] Platform architect available during migration
- [ ] War room (Slack/Teams channel) created
- [ ] Rollback decision criteria agreed upon
- [ ] Post-migration monitoring schedule defined

---

## Migration Day Timeline

### T-60 Minutes: Final Preparation

**Time:** 5:00 AM UTC (1 hour before start)
**Owner:** DevOps Lead

**Actions:**

1. **Join war room**
   ```bash
   # Slack channel: #migration-war-room
   # Post: "Migration team assembling. Go/No-Go in 30 minutes."
   ```

2. **Verify infrastructure health**
   ```bash
   # Check production API health
   curl https://api.gymnastics.example.com/health | jq

   # Check database connectivity
   psql $PROD_DB_CONNECTION -c "SELECT version();"

   # Check Azure Key Vault access
   az keyvault secret show --vault-name gymnastics-kv --name EntraApiClientSecret
   ```

3. **Deploy migration artifacts to staging (final verification)**
   ```bash
   # Backend
   cd src/GymnasticsPlatform.Api
   dotnet publish -c Release -o ./publish

   # Frontend
   cd frontend/user-portal
   npm run build:production
   ```

4. **Take final database backup**
   ```bash
   # PostgreSQL backup
   pg_dump -h prod-db.postgres.azure.com \
     -U admin \
     -d gymnastics_prod \
     -F c \
     -f ./backups/pre-migration-$(date +%Y%m%d-%H%M%S).backup

   # Verify backup size
   ls -lh ./backups/
   ```

5. **Test rollback procedure in staging**
   ```bash
   # Switch back to Keycloak
   cd staging-api
   vim appsettings.json  # ActiveProvider = "Keycloak"
   systemctl restart gymnastics-api

   # Verify Keycloak login works
   curl -X POST https://staging-api.gymnastics.example.com/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"test@example.com","password":"Test123!"}'

   # Switch back to Entra for migration prep
   vim appsettings.json  # ActiveProvider = "EntraId"
   systemctl restart gymnastics-api
   ```

### T-30 Minutes: Go/No-Go Decision

**Time:** 5:30 AM UTC
**Owner:** Platform Architect / Tech Lead

**Go/No-Go Checklist:**

- [ ] All team members present in war room
- [ ] Infrastructure health green (API, DB, Azure services)
- [ ] Database backup verified
- [ ] Rollback tested in staging
- [ ] No ongoing incidents in production
- [ ] On-call coverage confirmed for next 24 hours

**Decision:**

```
Platform Architect: "Go/No-Go poll:"
DevOps Lead: "Go"
QA Lead: "Go"
Security Lead: "Go"
Product Owner: "Go"

Platform Architect: "We are GO for migration. Proceeding at T-0 (6:00 AM UTC)."
```

**If No-Go:**
- Document reason for abort
- Reschedule migration
- Update user communications
- Exit war room

---

### T-0: Migration Start

**Time:** 6:00 AM UTC
**Duration:** ~2 hours
**Owner:** DevOps Lead

#### Step 1: Enable Maintenance Mode (6:00 AM)

**Purpose:** Prevent new user sessions during migration

```bash
# Update load balancer to return 503
# Azure App Service example:
az webapp stop --name gymnastics-api --resource-group gymnastics-prod

# Or NGINX maintenance page
sudo cp /etc/nginx/sites-available/maintenance.conf \
       /etc/nginx/sites-enabled/default
sudo systemctl reload nginx
```

**Verify maintenance page visible:**
```bash
curl https://api.gymnastics.example.com
# Expected: 503 Service Unavailable
```

**Post to status page:**
```
Status: Maintenance
Message: "We're performing scheduled maintenance to improve authentication.
Expected duration: 2 hours. Thank you for your patience!"
```

#### Step 2: Stop API Servers (6:05 AM)

**Purpose:** Drain existing connections and stop processing requests

```bash
# Graceful shutdown (wait for active requests to complete)
# Azure App Service
az webapp stop --name gymnastics-api --resource-group gymnastics-prod

# Or systemd service
sudo systemctl stop gymnastics-api

# Wait 30 seconds for connections to drain
sleep 30

# Verify API stopped
curl https://api.gymnastics.example.com/health
# Expected: Connection refused or 503
```

**Log to war room:**
```
DevOps Lead: "API servers stopped. Active sessions drained. ✓"
```

#### Step 3: Apply Database Migration (6:10 AM)

**Purpose:** Ensure schema matches new ProviderUserId naming (should be no-op if already applied)

```bash
# Connect to production database
psql $PROD_DB_CONNECTION

# Verify migration status
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId" DESC
LIMIT 5;

# Expected: Migration xxxxx_IncreaseProviderUserIdLength should be present
```

**If migration not applied:**
```bash
cd src/GymnasticsPlatform.Api

# Apply migration
dotnet ef database update \
  --connection "$PROD_DB_CONNECTION" \
  --context AuthDbContext

# Verify
psql $PROD_DB_CONNECTION -c "
  SELECT column_name, data_type, character_maximum_length
  FROM information_schema.columns
  WHERE table_name = 'UserProfiles' AND column_name = 'provider_user_id';
"
# Expected: provider_user_id | character varying | 255
```

**Log to war room:**
```
DevOps Lead: "Database migration verified. Schema ready for Entra ID. ✓"
```

#### Step 4: Update Configuration (6:15 AM)

**Purpose:** Switch authentication provider from Keycloak to Entra ID

**Backend:**

```bash
# SSH to production API server
ssh prod-api-server

# Backup current config
sudo cp /var/www/gymnastics-api/appsettings.json \
        /var/www/gymnastics-api/appsettings.json.backup

# Update ActiveProvider
sudo vim /var/www/gymnastics-api/appsettings.json

# Change this line:
#   "ActiveProvider": "Keycloak",
# To:
#   "ActiveProvider": "EntraId",

# Verify change
cat /var/www/gymnastics-api/appsettings.json | jq '.Authentication.ActiveProvider'
# Expected: "EntraId"
```

**Frontend:**

```bash
# Update environment variables (Azure App Service)
az webapp config appsettings set \
  --name gymnastics-app \
  --resource-group gymnastics-prod \
  --settings \
    VITE_AUTH_PROVIDER=entra \
    VITE_ENTRA_CLIENT_ID=$ENTRA_CLIENT_ID \
    VITE_ENTRA_TENANT_ID=$ENTRA_TENANT_ID \
    VITE_REDIRECT_URI=https://app.gymnastics.example.com/auth/callback

# Or update .env file on server
sudo vim /var/www/gymnastics-app/.env
# Set:
#   VITE_AUTH_PROVIDER=entra
#   VITE_ENTRA_CLIENT_ID=...
#   VITE_ENTRA_TENANT_ID=...
#   VITE_REDIRECT_URI=https://app.gymnastics.example.com/auth/callback
```

**Log to war room:**
```
DevOps Lead: "Configuration updated. ActiveProvider=EntraId. ✓"
```

#### Step 5: Deploy New Backend (6:20 AM)

**Purpose:** Deploy backend configured for Entra ID

```bash
# Deploy via Azure App Service
az webapp deployment source config-zip \
  --resource-group gymnastics-prod \
  --name gymnastics-api \
  --src ./publish.zip

# Or copy files to server
scp -r ./publish/* prod-api-server:/var/www/gymnastics-api/

# Wait for deployment
sleep 30
```

**Verify deployment:**
```bash
# Check file timestamps
ssh prod-api-server "ls -lh /var/www/gymnastics-api/ | head"

# Check DLL version
ssh prod-api-server "strings /var/www/gymnastics-api/GymnasticsPlatform.Api.dll | grep 'Version'"
```

**Log to war room:**
```
DevOps Lead: "Backend deployed. Version: [commit SHA]. ✓"
```

#### Step 6: Deploy New Frontend (6:25 AM)

**Purpose:** Deploy frontend configured for MSAL/Entra ID

```bash
# Build frontend with production config
cd frontend/user-portal
npm run build:production

# Deploy to Azure Static Web Apps
az staticwebapp upload \
  --name gymnastics-app \
  --resource-group gymnastics-prod \
  --source ./dist

# Or copy to CDN
aws s3 sync ./dist s3://gymnastics-frontend-prod --delete

# Clear CDN cache
az cdn endpoint purge \
  --resource-group gymnastics-prod \
  --profile-name gymnastics-cdn \
  --name app \
  --content-paths "/*"
```

**Verify deployment:**
```bash
# Check index.html timestamp
curl -I https://app.gymnastics.example.com

# Verify MSAL config loaded
curl https://app.gymnastics.example.com/assets/index-[hash].js | grep "VITE_AUTH_PROVIDER"
# Should contain: VITE_AUTH_PROVIDER=entra
```

**Log to war room:**
```
DevOps Lead: "Frontend deployed. MSAL configured. ✓"
```

#### Step 7: Start API Servers (6:30 AM)

**Purpose:** Bring API back online with Entra ID provider

```bash
# Start API service
# Azure App Service
az webapp start --name gymnastics-api --resource-group gymnastics-prod

# Or systemd
sudo systemctl start gymnastics-api

# Wait for warmup
sleep 15
```

**Verify startup:**
```bash
# Check health endpoint
curl https://api.gymnastics.example.com/health | jq
# Expected: {"status": "Healthy", "provider": "EntraId"}

# Check logs for provider confirmation
az webapp log tail --name gymnastics-api --resource-group gymnastics-prod | grep "Active authentication provider"
# Expected: "Active authentication provider: EntraId"
```

**Log to war room:**
```
DevOps Lead: "API servers online. Health check GREEN. Entra ID active. ✓"
```

#### Step 8: Smoke Test (6:35 AM)

**Purpose:** Verify core flows work before opening to users

**Email/Password Login:**
```bash
# Create test account via Entra ID (if not exists)
curl -X POST https://api.gymnastics.example.com/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "smoketest@gymnastics.example.com",
    "password": "SmokeTest123!",
    "fullName": "Smoke Test User"
  }'

# Wait for email verification (check inbox)
# Click verification link

# Login
TOKEN_RESPONSE=$(curl -s -X POST https://api.gymnastics.example.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "smoketest@gymnastics.example.com",
    "password": "SmokeTest123!"
  }')

echo $TOKEN_RESPONSE | jq

# Expected: {accessToken, refreshToken, user: {email, fullName}}

# Extract token
TOKEN=$(echo $TOKEN_RESPONSE | jq -r '.accessToken')

# Call protected endpoint
curl -H "Authorization: Bearer $TOKEN" \
  https://api.gymnastics.example.com/api/auth/me | jq

# Expected: {userId, email, tenantId, roles}
```

**Google OAuth Login:**
```bash
# Manual test in browser:
# 1. Open https://app.gymnastics.example.com/sign-in
# 2. Click "Sign in with Google"
# 3. Should redirect to Entra ID
# 4. Should federate to Google
# 5. Should redirect back to app
# 6. Should see dashboard

# Verify in browser console:
console.log("Auth Provider:", import.meta.env.VITE_AUTH_PROVIDER)
# Expected: "entra"
```

**Microsoft OAuth Login:**
```bash
# Manual test in browser:
# 1. Open https://app.gymnastics.example.com/sign-in
# 2. Click "Sign in with Microsoft"
# 3. Should redirect to Entra ID login
# 4. Should redirect back to app
# 5. Should see dashboard
```

**Multi-Tenancy:**
```bash
# Verify tenant ID in JWT
echo $TOKEN | cut -d'.' -f2 | base64 -d | jq '.extension_xxx_tenant_id'
# Expected: tenant GUID (not onboarding tenant if user completed onboarding)
```

**Checklist:**
- [ ] Email/password registration works
- [ ] Email/password login works
- [ ] Google OAuth login works
- [ ] Microsoft OAuth login works
- [ ] /api/auth/me returns correct data
- [ ] Session persists across page refresh
- [ ] Tenant ID present in JWT

**If any test fails:**
- DO NOT proceed to Step 9
- Execute rollback immediately (see [Rollback Procedure](#rollback-procedure))
- Document failure in war room

**Log to war room:**
```
DevOps Lead: "Smoke tests PASSED. All core flows working. ✓"
QA Lead: "Verified email/password, Google OAuth, Microsoft OAuth all functional. ✓"
```

#### Step 9: Disable Maintenance Mode (6:45 AM)

**Purpose:** Open platform to users

```bash
# Remove maintenance page
# Azure App Service already started in Step 7

# Or NGINX
sudo rm /etc/nginx/sites-enabled/maintenance.conf
sudo ln -s /etc/nginx/sites-available/production.conf \
            /etc/nginx/sites-enabled/default
sudo systemctl reload nginx
```

**Verify site accessible:**
```bash
curl -I https://api.gymnastics.example.com
# Expected: 200 OK

curl -I https://app.gymnastics.example.com
# Expected: 200 OK
```

**Update status page:**
```
Status: Operational
Message: "Maintenance complete. Authentication upgraded to Microsoft Entra ID.
All users will need to log in again."
```

**Post announcement:**
```
Subject: Platform Maintenance Complete

The scheduled maintenance is now complete. We've upgraded our authentication
system for improved security and reliability.

What you need to know:
- You will be logged out and need to sign in again
- Use your existing email and password
- Google Sign-In and Microsoft Sign-In now available
- Contact support@gymnastics.example.com if you have any issues

Thank you for your patience!
```

**Log to war room:**
```
DevOps Lead: "Maintenance mode disabled. Platform LIVE. ✓"
Platform Architect: "Migration phase complete. Moving to monitoring phase."
```

---

### T+30 Minutes: Initial Monitoring (7:15 AM)

**Owner:** DevOps + Platform Team

**Metrics to Monitor:**

1. **Authentication Success Rate**
   ```kusto
   // Application Insights Query
   customMetrics
   | where name == "auth.authentication.attempts"
   | summarize Attempts = sum(value) by bin(timestamp, 5m)
   | join kind=leftouter (
       customMetrics
       | where name == "auth.authentication.failures"
       | summarize Failures = sum(value) by bin(timestamp, 5m)
     ) on timestamp
   | extend SuccessRate = 100.0 * (Attempts - Failures) / Attempts
   | project timestamp, Attempts, Failures, SuccessRate
   ```

   **Target:** Success rate > 95%

2. **Active User Sessions**
   ```kusto
   traces
   | where message contains "User authenticated successfully"
   | summarize UniqueUsers = dcount(customDimensions.userId) by bin(timestamp, 5m)
   | project timestamp, UniqueUsers
   ```

   **Target:** Steadily increasing (users logging back in)

3. **API Error Rate**
   ```kusto
   requests
   | where resultCode >= 400
   | summarize ErrorCount = count() by bin(timestamp, 5m), resultCode
   | project timestamp, resultCode, ErrorCount
   ```

   **Target:** Error rate < 5%

4. **Token Refresh Success Rate**
   ```kusto
   customMetrics
   | where name == "auth.token.refresh.success"
   | summarize SuccessCount = sum(value) by bin(timestamp, 5m)
   ```

   **Target:** > 99% success rate

**Checklist:**
- [ ] Authentication success rate > 95%
- [ ] Users successfully logging in (rising active sessions)
- [ ] API error rate < 5%
- [ ] No P1 incidents in monitoring system
- [ ] Support queue manageable (<10 tickets)

**Log to war room:**
```
DevOps Lead: "T+30 monitoring check. Metrics within normal range. ✓"
  - Auth success rate: 97.2%
  - Active users: 45 (growing)
  - API errors: 2.1%
  - Support tickets: 3

Platform Architect: "Looking good. Continue monitoring."
```

---

### T+60 Minutes: 1-Hour Check (8:00 AM)

**Owner:** Platform Team

**Deep Dive:**

1. **User Feedback**
   - Check support tickets for patterns
   - Monitor social media / community forums
   - Review #migration-feedback Slack channel

2. **Performance**
   ```kusto
   // P95 login latency
   customMetrics
   | where name == "auth.authentication.duration"
   | summarize P95 = percentile(value, 95) by bin(timestamp, 15m)
   | project timestamp, P95
   ```

   **Target:** P95 < 2000ms

3. **Provider Distribution**
   ```kusto
   traces
   | where message contains "authenticated successfully"
   | summarize Count = count() by tostring(customDimensions.provider)
   | project provider, Count
   ```

   **Expected:** 100% Entra ID (no Keycloak logins)

4. **Tenant Resolution**
   ```sql
   -- Database query: Users with onboarding tenant (should be minimal)
   SELECT COUNT(*)
   FROM "UserProfiles"
   WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'::uuid;
   ```

   **Expected:** Only brand new users (created after migration)

**Checklist:**
- [ ] No critical bugs reported
- [ ] Performance within targets
- [ ] 100% of logins via Entra ID
- [ ] Support tickets under control
- [ ] No rollback triggers activated

**Log to war room:**
```
Platform Architect: "1-hour checkpoint. Migration successful. ✓"
  - 150 users logged in successfully
  - No critical issues
  - Performance excellent (P95 = 1.2s)
  - Support team handling minor issues well

DevOps Lead: "Recommend standing down war room in 1 hour if stable."
```

---

### T+120 Minutes: All Clear (9:00 AM)

**Owner:** Platform Architect

**Final Validation:**

- [ ] 200+ users logged in successfully
- [ ] Authentication success rate stable at >95%
- [ ] No P1 incidents
- [ ] Performance metrics within targets
- [ ] Support team reports no blocking issues

**Decision:**

```
Platform Architect: "Migration validated successful. Standing down war room."
  - Total users migrated: 500+
  - Success rate: 97.8%
  - Incidents: 0 critical, 2 minor (resolved)
  - Status: GREEN

DevOps Lead: "War room stood down. Moving to standard on-call monitoring."

Platform Architect: "Excellent work everyone. Post-mortem scheduled for Monday."
```

**Post to status page:**
```
Status: Operational
Message: "Migration to Microsoft Entra ID completed successfully.
All systems operating normally. Thank you for your patience!"
```

**Send success email:**
```
Subject: Migration Successful - Thank You

Our authentication upgrade is complete! Over 500 users have successfully
logged in using the new system.

New features:
✓ Sign in with Microsoft (in addition to Google)
✓ Improved security and reliability
✓ Faster login times

If you haven't logged in yet, your existing credentials still work.
Just visit app.gymnastics.example.com and sign in as usual.

Thank you for your support!
```

---

## Rollback Procedure

**CRITICAL:** Execute rollback immediately if any of these conditions occur:

- Authentication success rate < 80% for 15+ minutes
- P1 incident declared (platform unusable)
- Critical security issue discovered
- Data loss detected
- Smoke tests fail during Step 8

### Rollback Steps (15 Minutes)

#### 1. Enable Maintenance Mode (Immediately)

```bash
# Stop API
az webapp stop --name gymnastics-api --resource-group gymnastics-prod
```

**Post to war room:**
```
Platform Architect: "ROLLBACK INITIATED. Reason: [specific reason]"
DevOps Lead: "Enabling maintenance mode NOW."
```

#### 2. Restore Configuration (5 Minutes)

**Backend:**
```bash
# SSH to API server
ssh prod-api-server

# Restore Keycloak config
sudo cp /var/www/gymnastics-api/appsettings.json.backup \
        /var/www/gymnastics-api/appsettings.json

# Verify
cat /var/www/gymnastics-api/appsettings.json | jq '.Authentication.ActiveProvider'
# Expected: "Keycloak"
```

**Frontend:**
```bash
# Revert environment variables
az webapp config appsettings set \
  --name gymnastics-app \
  --resource-group gymnastics-prod \
  --settings \
    VITE_AUTH_PROVIDER=keycloak \
    VITE_KEYCLOAK_URL=$KEYCLOAK_URL \
    VITE_KEYCLOAK_REALM=gymnastics \
    VITE_KEYCLOAK_CLIENT_ID=user-portal
```

#### 3. Restart API (2 Minutes)

```bash
# Start API with Keycloak config
az webapp start --name gymnastics-api --resource-group gymnastics-prod

# Wait for warmup
sleep 15

# Verify Keycloak active
curl https://api.gymnastics.example.com/health | jq '.provider'
# Expected: "Keycloak"
```

#### 4. Smoke Test Keycloak (3 Minutes)

```bash
# Login via Keycloak
curl -X POST https://api.gymnastics.example.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }' | jq
```

**Checklist:**
- [ ] Keycloak login works
- [ ] Session cookie set
- [ ] /api/auth/me returns data
- [ ] No errors in logs

#### 5. Disable Maintenance Mode (1 Minute)

```bash
# Frontend already redeployed in Step 2
# API already started in Step 3

# Update status page
# Status: Operational
# Message: "Migration postponed due to technical issues.
#          Platform operating normally on previous system."
```

#### 6. Post-Rollback Communication

**Immediate (within 15 minutes):**
```
Subject: Migration Postponed

Due to technical issues during today's maintenance, we've postponed the
authentication upgrade. The platform is now fully operational on the
previous system.

You can continue using the platform as normal. We'll communicate a new
migration date soon.

We apologize for the inconvenience.
```

**Post-Mortem (within 24 hours):**
- Document root cause of rollback
- Identify fixes needed
- Schedule new migration date
- Update runbook with lessons learned

---

## Post-Migration Monitoring (7 Days)

### Daily Checks

**Days 1-3: Active Monitoring**
- Check authentication success rate every 4 hours
- Review support tickets twice daily
- Monitor performance metrics continuously

**Days 4-7: Reduced Monitoring**
- Check authentication success rate daily
- Review support tickets daily
- Standard performance monitoring

### Metrics to Track

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Auth success rate | > 95% | < 90% |
| P95 login latency | < 2s | > 3s |
| API error rate | < 5% | > 10% |
| Token refresh failures | < 1% | > 5% |
| Support tickets/day | < 10 | > 20 |

### Weekly Review (Day 7)

**Checklist:**
- [ ] No critical incidents in 7 days
- [ ] All metrics within targets
- [ ] Support ticket volume normalized
- [ ] No outstanding migration-related bugs
- [ ] User feedback predominantly positive

**If all checks pass:**
- Migration considered successful
- Proceed to Phase 7 (Cleanup)
- Schedule Keycloak decommission

**If issues remain:**
- Extend monitoring period
- Address outstanding issues
- Delay Phase 7 until stable

---

## Emergency Contacts

| Role | Name | Phone | Email |
|------|------|-------|-------|
| Platform Architect | [Name] | [Phone] | [Email] |
| DevOps Lead | [Name] | [Phone] | [Email] |
| QA Lead | [Name] | [Phone] | [Email] |
| Security Lead | [Name] | [Phone] | [Email] |
| Product Owner | [Name] | [Phone] | [Email] |

**Escalation Path:**
1. DevOps Lead (first 30 minutes)
2. Platform Architect (if unresolved after 30 min)
3. CTO (if critical incident >1 hour)

---

## Appendices

### Appendix A: Keycloak vs Entra ID Quick Reference

| Feature | Keycloak | Entra ID |
|---------|----------|----------|
| Login URL | `https://keycloak.example.com/realms/gymnastics` | `https://login.microsoftonline.com/{tenant}` |
| Token endpoint | `/protocol/openid-connect/token` | `/oauth2/v2.0/token` |
| User attribute (tenant) | `tenant_id` (custom attribute) | `extension_{app}_tenant_id` |
| OAuth providers | Google | Google + Microsoft |

### Appendix B: Common Post-Migration Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "Invalid redirect URI" | SPA redirect not registered | Add to app registration |
| Session expires immediately | Clock skew | Sync server clocks with NTP |
| `tenant_id` missing in JWT | Optional claims not configured | Add to token configuration |
| Token refresh fails | Refresh token expired | Force interactive login |
| CORS error | Frontend origin not allowed | Update CORS policy |

### Appendix C: Validation Queries

**Count users by provider:**
```sql
SELECT
  CASE
    WHEN "ProviderUserId" LIKE '%-%' THEN 'EntraId'
    ELSE 'Keycloak'
  END AS provider,
  COUNT(*) AS user_count
FROM "UserProfiles"
GROUP BY provider;
```

**Recent logins by provider:**
```sql
SELECT
  al."EntityId" AS provider_user_id,
  al."PerformedAt",
  up."Email"
FROM "AuditLogs" al
JOIN "UserProfiles" up ON al."EntityId" = up."ProviderUserId"
WHERE al."Action" = 'UserAuthenticated'
  AND al."PerformedAt" > NOW() - INTERVAL '1 hour'
ORDER BY al."PerformedAt" DESC
LIMIT 100;
```

---

**Document Version:** 1.0
**Last Updated:** 2026-04-08
**Next Review:** After Phase 5 testing completion
