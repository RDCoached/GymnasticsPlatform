#!/bin/bash
set -e

REALM="gymnastics"
TENANT_A_ID="a1111111-1111-1111-1111-111111111111"
TENANT_B_ID="b2222222-2222-2222-2222-222222222222"

echo "🔐 Configuring Keycloak using CLI..."

# Helper function to run kcadm commands
kcadm() {
  docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh "$@"
}

# Login
echo "📝 Logging in to Keycloak..."
kcadm config credentials --server http://localhost:8080 --realm master --user admin --password admin

# Create gymnastics realm
echo "📝 Creating realm: $REALM..."
kcadm create realms -s realm=$REALM -s enabled=true -s displayName="Gymnastics Platform" \
  -s accessTokenLifespan=3600 -s sslRequired=NONE -s registrationAllowed=false \
  -s loginWithEmailAllowed=true -s duplicateEmailsAllowed=false 2>/dev/null || echo "  ⚠️  Realm may already exist"

echo "✅ Realm created/verified"

# Create realm roles
echo "📝 Creating realm roles..."
kcadm create roles -r $REALM -s name=user -s 'description=Regular user role' 2>/dev/null || true
kcadm create roles -r $REALM -s name=organization_owner -s 'description=Organization owner role' 2>/dev/null || true
kcadm create roles -r $REALM -s name=platform_admin -s 'description=Platform administrator role' 2>/dev/null || true

echo "✅ Roles created"

# Create tenant_id client scope
echo "📝 Creating tenant_id client scope..."
SCOPE_ID=$(kcadm create client-scopes -r $REALM -s name=tenant_id \
  -s 'description=Tenant ID claim for multi-tenancy' \
  -s protocol=openid-connect \
  -s 'attributes.include.in.token.scope=true' \
  -s 'attributes.display.on.consent.screen=false' -i 2>/dev/null || \
  kcadm get client-scopes -r $REALM --fields id,name | grep -A1 '"name" : "tenant_id"' | grep '"id"' | cut -d'"' -f4)

echo "✅ Client scope created (ID: $SCOPE_ID)"

# Add protocol mapper to tenant_id scope
echo "📝 Adding protocol mapper for tenant_id..."
kcadm create client-scopes/$SCOPE_ID/protocol-mappers/models -r $REALM \
  -s name=tenant-id-mapper \
  -s protocol=openid-connect \
  -s protocolMapper=oidc-usermodel-attribute-mapper \
  -s 'config."user.attribute"=tenant_id' \
  -s 'config."claim.name"=tenant_id' \
  -s 'config."jsonType.label"=String' \
  -s 'config."id.token.claim"=true' \
  -s 'config."access.token.claim"=true' \
  -s 'config."userinfo.token.claim"=true' 2>/dev/null || echo "  ⚠️  Mapper may already exist"

echo "✅ Protocol mapper added"

# Create gymnastics-api client (bearer-only for API validation)
echo "📝 Creating client: gymnastics-api..."
kcadm create clients -r $REALM \
  -s clientId=gymnastics-api \
  -s name="Gymnastics Platform API" \
  -s enabled=true \
  -s publicClient=false \
  -s bearerOnly=true \
  -s standardFlowEnabled=false \
  -s directAccessGrantsEnabled=false \
  -s protocol=openid-connect 2>/dev/null || echo "  ⚠️  Client may already exist"

echo "✅ gymnastics-api client created"

# Create user-portal client (public SPA)
echo "📝 Creating client: user-portal..."
USER_PORTAL_ID=$(kcadm create clients -r $REALM \
  -s clientId=user-portal \
  -s name="User Portal" \
  -s enabled=true \
  -s publicClient=true \
  -s standardFlowEnabled=true \
  -s directAccessGrantsEnabled=false \
  -s 'webOrigins=["http://localhost:3001"]' \
  -s 'redirectUris=["http://localhost:3001/*"]' \
  -s protocol=openid-connect -i 2>/dev/null || \
  kcadm get clients -r $REALM --fields id,clientId | grep -A1 '"clientId" : "user-portal"' | grep '"id"' | cut -d'"' -f4)

# Add tenant_id scope to user-portal
kcadm update clients/$USER_PORTAL_ID/default-client-scopes/$SCOPE_ID -r $REALM 2>/dev/null || true

echo "✅ user-portal client created"

# Create admin-portal client (public SPA)
echo "📝 Creating client: admin-portal..."
ADMIN_PORTAL_ID=$(kcadm create clients -r $REALM \
  -s clientId=admin-portal \
  -s name="Admin Portal" \
  -s enabled=true \
  -s publicClient=true \
  -s standardFlowEnabled=true \
  -s directAccessGrantsEnabled=false \
  -s 'webOrigins=["http://localhost:3002"]' \
  -s 'redirectUris=["http://localhost:3002/*"]' \
  -s protocol=openid-connect -i 2>/dev/null || \
  kcadm get clients -r $REALM --fields id,clientId | grep -A1 '"clientId" : "admin-portal"' | grep '"id"' | cut -d'"' -f4)

# Add tenant_id scope to admin-portal
kcadm update clients/$ADMIN_PORTAL_ID/default-client-scopes/$SCOPE_ID -r $REALM 2>/dev/null || true

echo "✅ admin-portal client created"

# Create test users
echo "📝 Creating test users..."

# Test User 1 (Tenant A - user role)
echo "  Creating user@tenanta.com..."
USER1_ID=$(kcadm create users -r $REALM \
  -s username=user@tenanta.com \
  -s email=user@tenanta.com \
  -s enabled=true \
  -s emailVerified=true \
  -s 'attributes.tenant_id=["'"$TENANT_A_ID"'"]' -i 2>/dev/null || \
  kcadm get users -r $REALM -q username=user@tenanta.com --fields id | grep '"id"' | head -1 | cut -d'"' -f4)

kcadm set-password -r $REALM --username user@tenanta.com --new-password Test123! 2>/dev/null || true
kcadm add-roles -r $REALM --uusername user@tenanta.com --rolename user 2>/dev/null || true

echo "✅ Created user@tenanta.com (Tenant A: $TENANT_A_ID)"

# Test User 2 (Tenant B - org owner)
echo "  Creating owner@tenantb.com..."
USER2_ID=$(kcadm create users -r $REALM \
  -s username=owner@tenantb.com \
  -s email=owner@tenantb.com \
  -s enabled=true \
  -s emailVerified=true \
  -s 'attributes.tenant_id=["'"$TENANT_B_ID"'"]' -i 2>/dev/null || \
  kcadm get users -r $REALM -q username=owner@tenantb.com --fields id | grep '"id"' | head -1 | cut -d'"' -f4)

kcadm set-password -r $REALM --username owner@tenantb.com --new-password Test123! 2>/dev/null || true
kcadm add-roles -r $REALM --uusername owner@tenantb.com --rolename organization_owner 2>/dev/null || true

echo "✅ Created owner@tenantb.com (Tenant B: $TENANT_B_ID)"

# Test User 3 (Platform Admin - no tenant)
echo "  Creating admin@platform.com..."
USER3_ID=$(kcadm create users -r $REALM \
  -s username=admin@platform.com \
  -s email=admin@platform.com \
  -s enabled=true \
  -s emailVerified=true -i 2>/dev/null || \
  kcadm get users -r $REALM -q username=admin@platform.com --fields id | grep '"id"' | head -1 | cut -d'"' -f4)

kcadm set-password -r $REALM --username admin@platform.com --new-password Test123! 2>/dev/null || true
kcadm add-roles -r $REALM --uusername admin@platform.com --rolename platform_admin 2>/dev/null || true

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
echo "  Realm: $REALM"
echo "  Admin: admin / admin"
echo ""
echo "🔑 Token endpoint for testing:"
echo "  http://localhost:8080/realms/$REALM/protocol/openid-connect/token"
echo ""
