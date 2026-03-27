#!/bin/bash
set -e

KEYCLOAK_URL="http://localhost:8080"
ADMIN_USER="admin"
ADMIN_PASSWORD="admin"
REALM="gymnastics"

echo "🔐 Configuring Keycloak..."

# Get admin access token
echo "📝 Getting admin access token..."
ADMIN_TOKEN=$(curl -s -X POST "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=$ADMIN_USER" \
  -d "password=$ADMIN_PASSWORD" \
  -d "grant_type=password" \
  -d "client_id=admin-cli" | jq -r '.access_token')

if [ "$ADMIN_TOKEN" == "null" ] || [ -z "$ADMIN_TOKEN" ]; then
  echo "❌ Failed to get admin token"
  exit 1
fi

echo "✅ Admin token obtained"

# Create gymnastics realm
echo "📝 Creating realm: $REALM..."
curl -s -X POST "$KEYCLOAK_URL/admin/realms" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "realm": "'"$REALM"'",
    "enabled": true,
    "displayName": "Gymnastics Platform",
    "accessTokenLifespan": 3600,
    "sslRequired": "none",
    "registrationAllowed": false,
    "loginWithEmailAllowed": true,
    "duplicateEmailsAllowed": false
  }' || echo "⚠️  Realm may already exist"

echo "✅ Realm created/verified"

# Create tenant_id client scope
echo "📝 Creating tenant_id client scope..."
SCOPE_RESPONSE=$(curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/client-scopes" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "tenant_id",
    "description": "Tenant ID claim for multi-tenancy",
    "protocol": "openid-connect",
    "attributes": {
      "include.in.token.scope": "true",
      "display.on.consent.screen": "false"
    }
  }')

# Get the tenant_id scope ID
TENANT_SCOPE_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/client-scopes" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[] | select(.name=="tenant_id") | .id')

echo "✅ Client scope created (ID: $TENANT_SCOPE_ID)"

# Add protocol mapper to tenant_id scope
echo "📝 Adding protocol mapper for tenant_id..."
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/client-scopes/$TENANT_SCOPE_ID/protocol-mappers/models" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "tenant-id-mapper",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-usermodel-attribute-mapper",
    "config": {
      "user.attribute": "tenant_id",
      "claim.name": "tenant_id",
      "jsonType.label": "String",
      "id.token.claim": "true",
      "access.token.claim": "true",
      "userinfo.token.claim": "true"
    }
  }'

echo "✅ Protocol mapper added"

# Create gymnastics-api client
echo "📝 Creating client: gymnastics-api..."
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/clients" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "gymnastics-api",
    "name": "Gymnastics Platform API",
    "enabled": true,
    "publicClient": false,
    "bearerOnly": true,
    "standardFlowEnabled": false,
    "directAccessGrantsEnabled": false,
    "serviceAccountsEnabled": false,
    "protocol": "openid-connect"
  }'

echo "✅ gymnastics-api client created"

# Create user-portal client
echo "📝 Creating client: user-portal..."
USER_PORTAL_RESPONSE=$(curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/clients" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "user-portal",
    "name": "User Portal",
    "enabled": true,
    "publicClient": true,
    "standardFlowEnabled": true,
    "directAccessGrantsEnabled": false,
    "webOrigins": ["http://localhost:3001"],
    "redirectUris": ["http://localhost:3001/*"],
    "protocol": "openid-connect"
  }')

# Get user-portal client ID and add tenant_id scope
USER_PORTAL_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/clients" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[] | select(.clientId=="user-portal") | .id')

curl -s -X PUT "$KEYCLOAK_URL/admin/realms/$REALM/clients/$USER_PORTAL_ID/default-client-scopes/$TENANT_SCOPE_ID" \
  -H "Authorization: Bearer $ADMIN_TOKEN"

echo "✅ user-portal client created"

# Create admin-portal client
echo "📝 Creating client: admin-portal..."
ADMIN_PORTAL_RESPONSE=$(curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/clients" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "admin-portal",
    "name": "Admin Portal",
    "enabled": true,
    "publicClient": true,
    "standardFlowEnabled": true,
    "directAccessGrantsEnabled": false,
    "webOrigins": ["http://localhost:3002"],
    "redirectUris": ["http://localhost:3002/*"],
    "protocol": "openid-connect"
  }')

# Get admin-portal client ID and add tenant_id scope
ADMIN_PORTAL_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/clients" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[] | select(.clientId=="admin-portal") | .id')

curl -s -X PUT "$KEYCLOAK_URL/admin/realms/$REALM/clients/$ADMIN_PORTAL_ID/default-client-scopes/$TENANT_SCOPE_ID" \
  -H "Authorization: Bearer $ADMIN_TOKEN"

echo "✅ admin-portal client created"

# Create realm roles
echo "📝 Creating realm roles..."
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "user",
    "description": "Regular user role"
  }'

curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "organization_owner",
    "description": "Organization owner role"
  }'

curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "platform_admin",
    "description": "Platform administrator role"
  }'

echo "✅ Roles created"

# Create test users
echo "📝 Creating test users..."

# Test User 1 (Tenant A - user role)
TENANT_A_ID="a1111111-1111-1111-1111-111111111111"
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "user@tenanta.com",
    "email": "user@tenanta.com",
    "enabled": true,
    "emailVerified": true,
    "attributes": {
      "tenant_id": ["'"$TENANT_A_ID"'"]
    },
    "credentials": [{
      "type": "password",
      "value": "Test123!",
      "temporary": false
    }]
  }'

# Get user ID and assign role
USER1_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/users?username=user@tenanta.com" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[0].id')

USER_ROLE_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/roles/user" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.id')

curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users/$USER1_ID/role-mappings/realm" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '[{
    "id": "'"$USER_ROLE_ID"'",
    "name": "user"
  }]'

echo "✅ Created user@tenanta.com (Tenant A)"

# Test User 2 (Tenant B - org owner)
TENANT_B_ID="b2222222-2222-2222-2222-222222222222"
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "owner@tenantb.com",
    "email": "owner@tenantb.com",
    "enabled": true,
    "emailVerified": true,
    "attributes": {
      "tenant_id": ["'"$TENANT_B_ID"'"]
    },
    "credentials": [{
      "type": "password",
      "value": "Test123!",
      "temporary": false
    }]
  }'

# Get user ID and assign role
USER2_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/users?username=owner@tenantb.com" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[0].id')

ORG_OWNER_ROLE_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/roles/organization_owner" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.id')

curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users/$USER2_ID/role-mappings/realm" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '[{
    "id": "'"$ORG_OWNER_ROLE_ID"'",
    "name": "organization_owner"
  }]'

echo "✅ Created owner@tenantb.com (Tenant B)"

# Test User 3 (Platform Admin - no tenant)
curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin@platform.com",
    "email": "admin@platform.com",
    "enabled": true,
    "emailVerified": true,
    "credentials": [{
      "type": "password",
      "value": "Test123!",
      "temporary": false
    }]
  }'

# Get user ID and assign role
USER3_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/users?username=admin@platform.com" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.[0].id')

ADMIN_ROLE_ID=$(curl -s "$KEYCLOAK_URL/admin/realms/$REALM/roles/platform_admin" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq -r '.id')

curl -s -X POST "$KEYCLOAK_URL/admin/realms/$REALM/users/$USER3_ID/role-mappings/realm" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '[{
    "id": "'"$ADMIN_ROLE_ID"'",
    "name": "platform_admin"
  }]'

echo "✅ Created admin@platform.com (Platform Admin)"

echo ""
echo "🎉 Keycloak configuration complete!"
echo ""
echo "📋 Summary:"
echo "  Realm: $REALM"
echo "  Clients: gymnastics-api, user-portal, admin-portal"
echo "  Roles: user, organization_owner, platform_admin"
echo ""
echo "👥 Test Users:"
echo "  user@tenanta.com / Test123! (Tenant A: $TENANT_A_ID)"
echo "  owner@tenantb.com / Test123! (Tenant B: $TENANT_B_ID)"
echo "  admin@platform.com / Test123! (Platform Admin)"
echo ""
echo "🌐 Access Keycloak Admin Console:"
echo "  URL: http://localhost:8080"
echo "  Username: admin"
echo "  Password: admin"
echo ""
