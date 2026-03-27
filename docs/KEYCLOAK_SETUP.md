# Keycloak Authentication Setup

## Overview

This platform uses Keycloak for authentication with Google OAuth integration and multi-tenant support.

## Key Configuration

### 1. Keycloak Environment (docker-compose.yml)

```yaml
keycloak:
  image: quay.io/keycloak/keycloak:26.0.7
  environment:
    KC_DB: postgres
    KC_DB_URL: jdbc:postgresql://postgres:5432/gymnastics
    KC_DB_USERNAME: gymadmin
    KC_DB_PASSWORD: local_dev_123
    KC_DB_SCHEMA: keycloak
    KC_BOOTSTRAP_ADMIN_USERNAME: admin
    KC_BOOTSTRAP_ADMIN_PASSWORD: admin
    KC_HTTP_ENABLED: "true"
    KC_HEALTH_ENABLED: "true"
  command: start-dev --import-realm
```

**CRITICAL**: Do NOT add hostname-related environment variables (`KC_HOSTNAME`, `KC_HOSTNAME_PORT`, etc.) as they cause HTTPS requirement issues in dev mode.

### 2. Master Realm SSL Configuration

**ISSUE**: By default, the master realm requires HTTPS, causing "HTTPS required" errors when accessing http://localhost:8080/admin/

**SOLUTION**: Disable SSL for master realm:
```bash
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update realms/master -s sslRequired=none
```

This must be done after every Keycloak reset/reinstall.

### 3. API Authentication Configuration

#### docker-compose.yml
```yaml
api:
  environment:
    - Authentication__Keycloak__Authority=http://keycloak:8080/realms/gymnastics
    - Authentication__Keycloak__Audience=gymnastics-api
    - Authentication__Keycloak__RequireHttpsMetadata=false
    - Authentication__Keycloak__ValidIssuers__0=http://keycloak:8080/realms/gymnastics
    - Authentication__Keycloak__ValidIssuers__1=http://localhost:8080/realms/gymnastics
```

**ValidIssuers**: Required to accept tokens from both internal Docker network (`keycloak:8080`) and external browser access (`localhost:8080`).

#### Program.cs
```csharp
// CRITICAL: Prevent JWT claim name mapping
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakConfig["Authority"];
        options.Audience = keycloakConfig["Audience"];
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false; // ← REQUIRED to preserve original claim names

        var validIssuers = keycloakConfig.GetSection("ValidIssuers").Get<string[]>();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuers = validIssuers
        };
    });
```

**MapInboundClaims = false**: Without this, claims are mapped:
- `sub` → `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`
- `email` → `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress`
- `roles` → `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`

## Required OIDC Protocol Mappers

All clients (user-portal, admin-portal) must have these mappers configured:

### Standard Claims
1. **sub** (User ID) - `oidc-usermodel-property-mapper`
   - User Attribute: `id`
   - Token Claim Name: `sub`
   - Add to: ID token, access token, userinfo

2. **email** - `oidc-usermodel-property-mapper`
   - User Attribute: `email`
   - Token Claim Name: `email`
   - Add to: ID token, access token, userinfo

3. **email_verified** - `oidc-usermodel-property-mapper`
   - User Attribute: `emailVerified`
   - Token Claim Name: `email_verified`
   - JSON Type: boolean

4. **preferred_username** - `oidc-usermodel-property-mapper`
   - User Attribute: `username`
   - Token Claim Name: `preferred_username`

5. **name** (Full Name) - `oidc-full-name-mapper`
   - Add to: ID token, access token, userinfo

### Authorization Claims
6. **aud** (Audience) - `oidc-audience-mapper`
   - Included Client Audience: `gymnastics-api`
   - Add to: access token only

7. **roles** - `oidc-usermodel-realm-role-mapper`
   - Token Claim Name: `roles`
   - Multivalued: true
   - Add to: ID token, access token, userinfo

### Multi-Tenancy
8. **tenant_id** - Hardcoded claim mapper via client scope
   - Client Scope Name: `tenant_id`
   - Claim Value: `00000000-0000-0000-0000-000000000001` (onboarding tenant GUID)
   - **CRITICAL**: Must be a valid GUID, not a string

**Automated Configuration**: Use `scripts/configure-oidc-clients-complete.sh` to configure all mappers automatically.

## Google OAuth Setup

### 1. Google Cloud Console
1. Create OAuth 2.0 Client ID at https://console.cloud.google.com/apis/credentials
2. Authorized redirect URI: `http://localhost:8080/realms/gymnastics/broker/google/endpoint`

### 2. Keycloak Configuration
1. Admin Console → gymnastics realm (NOT master)
2. Identity Providers → Add provider → Google
3. Client ID: `[your-google-client-id].apps.googleusercontent.com`
4. Client Secret: `GOCSPX-[your-secret]`
5. Save

**Common Mistake**: Adding Google IDP to master realm instead of gymnastics realm.

## Multi-Tenancy Architecture

### Tenant ID Format
- **Type**: GUID (UUID)
- **Onboarding Tenant**: `00000000-0000-0000-0000-000000000001`
- **Organization Tenants**: Generated UUIDs

### Data Model
- All multi-tenant entities implement `IMultiTenant` with `Guid TenantId`
- DbContext has global query filter: `e.TenantId == CurrentTenantId`
- SaveChanges automatically sets TenantId on new entities

### JWT Token Flow
1. User logs in with Google → Keycloak issues token
2. Token includes `tenant_id` claim (GUID)
3. API extracts tenant from `ITenantContext.TenantId`
4. Query filters automatically scope all queries to current tenant

## Troubleshooting

### "HTTPS required" Error
**Cause**: Master realm has `sslRequired` set to a value other than "none"

**Fix**:
```bash
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh config credentials \
    --server http://localhost:8080 --realm master --user admin --password admin
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update realms/master -s sslRequired=none
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update realms/gymnastics -s sslRequired=none
```

### "Invalid issuer" Error
**Cause**: Token issuer (`http://localhost:8080/realms/gymnastics`) doesn't match API authority

**Fix**: Ensure `ValidIssuers` includes both internal and external URLs in docker-compose.yml

### Claims are null in API
**Causes**:
1. **Missing `MapInboundClaims = false`** in JWT bearer options
2. **Missing protocol mappers** in Keycloak client
3. **User needs to log out/log in** after mapper changes

**Verification**: Add debug endpoint to see all claims:
```csharp
app.MapGet("/debug/claims", (HttpContext ctx) =>
    Results.Ok(ctx.User.Claims.Select(c => new { c.Type, c.Value })));
```

### tenant_id is null
**Cause**: tenant_id claim value is not a valid GUID

**Fix**: Update hardcoded mapper to use GUID format:
```bash
TENANT_SCOPE_ID=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh \
    get client-scopes -r gymnastics | jq -r '.[] | select(.name=="tenant_id") | .id')
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh create \
    client-scopes/$TENANT_SCOPE_ID/protocol-mappers/models -r gymnastics \
    -s name=hardcoded-onboarding-guid \
    -s protocol=openid-connect \
    -s protocolMapper=oidc-hardcoded-claim-mapper \
    -s 'config."claim.name"=tenant_id' \
    -s 'config."claim.value"=00000000-0000-0000-0000-000000000001' \
    -s 'config."jsonType.label"=String' \
    -s 'config."access.token.claim"=true' \
    -s 'config."id.token.claim"=true'
```

### Admin Account Locked
**Cause**: Too many failed login attempts trigger brute force protection

**Fix**: Restart Keycloak to clear locks:
```bash
docker-compose restart keycloak
```

## Testing Authentication

### Valid Token Response
```json
{
  "userId": "f91f78d4-ab90-4fb4-b3ce-a92aa64e2ea4",
  "email": "user@example.com",
  "name": "User Name",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "roles": ["offline_access", "uma_authorization", "user"]
}
```

### Test Endpoint
```
GET http://localhost:5001/api/auth/me
Authorization: Bearer [token]
```

## Important Files

- `docker-compose.yml` - Service configuration
- `src/GymnasticsPlatform.Api/Program.cs` - JWT authentication setup
- `src/GymnasticsPlatform.Api/Services/TenantContext.cs` - Tenant extraction from JWT
- `scripts/configure-oidc-clients-complete.sh` - Automated mapper configuration
- `docker/keycloak/` - Realm import configuration

## Reset Procedure

If Keycloak needs to be reset:

1. **Stop and remove Keycloak data**:
   ```bash
   docker-compose stop keycloak
   docker exec gymnastics-postgres psql -U gymadmin -d gymnastics \
       -c "DROP SCHEMA IF EXISTS keycloak CASCADE;"
   docker exec gymnastics-postgres psql -U gymadmin -d gymnastics \
       -c "CREATE SCHEMA keycloak;"
   docker-compose start keycloak
   ```

2. **Wait for startup** (~30 seconds)

3. **Disable SSL on master realm** (see above)

4. **Configure OIDC mappers**:
   ```bash
   bash scripts/configure-oidc-clients-complete.sh
   ```

5. **Add Google Identity Provider** through admin console

6. **Verify**: Log in with Google and test `/api/auth/me` endpoint
