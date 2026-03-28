#!/bin/bash
set -e

echo "=== Configuring Redirect URIs for Frontend Clients ==="

# Login to Keycloak
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh config credentials \
    --server http://localhost:8080 \
    --realm master \
    --user admin \
    --password admin > /dev/null 2>&1

# Get client IDs
USER_PORTAL_ID=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get clients -r gymnastics -q clientId=user-portal | jq -r '.[0].id')
ADMIN_PORTAL_ID=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get clients -r gymnastics -q clientId=admin-portal | jq -r '.[0].id')

echo "Configuring user-portal client..."
# Support ports 3001-3009 for local development flexibility
REDIRECT_URIS='["http://localhost:3001/*","http://localhost:3002/*","http://localhost:3003/*","http://localhost:3004/*","http://localhost:3005/*","http://localhost:3006/*","http://localhost:3007/*","http://localhost:3008/*","http://localhost:3009/*","http://localhost:5173/*","http://localhost:5174/*"]'
WEB_ORIGINS='["http://localhost:3001","http://localhost:3002","http://localhost:3003","http://localhost:3004","http://localhost:3005","http://localhost:3006","http://localhost:3007","http://localhost:3008","http://localhost:3009","http://localhost:5173","http://localhost:5174"]'

docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update clients/$USER_PORTAL_ID -r gymnastics \
    -s "redirectUris=$REDIRECT_URIS" \
    -s "webOrigins=$WEB_ORIGINS"

echo "Configuring admin-portal client..."
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update clients/$ADMIN_PORTAL_ID -r gymnastics \
    -s "redirectUris=$REDIRECT_URIS" \
    -s "webOrigins=$WEB_ORIGINS"

echo ""
echo "✅ Redirect URIs configured successfully!"
echo ""
echo "Both portals now accept ports 3001-3009 and Vite defaults (5173, 5174)"
