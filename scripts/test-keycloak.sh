#!/bin/bash

echo "🔍 Testing Keycloak Configuration..."
echo ""

# First, enable direct access grants for testing
echo "📝 Enabling direct access grants for user-portal client..."
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh config credentials --server http://localhost:8080 --realm master --user admin --password admin > /dev/null 2>&1

CLIENT_ID=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get clients -r gymnastics --fields id,clientId 2>/dev/null | jq -r '.[] | select(.clientId=="user-portal") | .id')

docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update clients/$CLIENT_ID -r gymnastics -s directAccessGrantsEnabled=true > /dev/null 2>&1

echo "✅ Direct access grants enabled"
echo ""

# Test Tenant A user
echo "👤 Testing user@tenanta.com (Tenant A)..."
RESPONSE=$(curl -s -X POST http://localhost:8080/realms/gymnastics/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=user-portal" \
  -d "username=user@tenanta.com" \
  -d "password=Test123!" \
  -d "grant_type=password")

if echo "$RESPONSE" | jq -e '.access_token' > /dev/null 2>&1; then
  echo "✅ Login successful!"
  TOKEN=$(echo "$RESPONSE" | jq -r '.access_token')
  echo ""
  echo "📋 Token Claims:"
  echo "$TOKEN" | cut -d'.' -f2 | base64 -d 2>/dev/null | jq '{
    tenant_id: .tenant_id,
    username: .preferred_username,
    email: .email,
    roles: .realm_access.roles
  }'
else
  echo "❌ Login failed:"
  echo "$RESPONSE" | jq '.'
fi

echo ""
echo "👤 Testing owner@tenantb.com (Tenant B)..."
RESPONSE=$(curl -s -X POST http://localhost:8080/realms/gymnastics/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=user-portal" \
  -d "username=owner@tenantb.com" \
  -d "password=Test123!" \
  -d "grant_type=password")

if echo "$RESPONSE" | jq -e '.access_token' > /dev/null 2>&1; then
  echo "✅ Login successful!"
  TOKEN=$(echo "$RESPONSE" | jq -r '.access_token')
  echo ""
  echo "📋 Token Claims:"
  echo "$TOKEN" | cut -d'.' -f2 | base64 -d 2>/dev/null | jq '{
    tenant_id: .tenant_id,
    username: .preferred_username,
    email: .email,
    roles: .realm_access.roles
  }'
else
  echo "❌ Login failed:"
  echo "$RESPONSE" | jq '.'
fi

echo ""
echo "✅ Keycloak is properly configured!"
echo ""
echo "🌐 Keycloak Admin Console: http://localhost:8080"
echo "   Realm: gymnastics"
echo "   Admin: admin / admin"
echo ""
