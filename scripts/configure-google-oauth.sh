#!/bin/bash

# Script to configure Google OAuth in Keycloak
# Usage: ./configure-google-oauth.sh <client-id> <client-secret>

set -e

if [ "$#" -ne 2 ]; then
    echo "Usage: $0 <google-client-id> <google-client-secret>"
    echo ""
    echo "To get Google OAuth credentials:"
    echo "1. Go to https://console.cloud.google.com/apis/credentials"
    echo "2. Create a new OAuth 2.0 Client ID (Web application)"
    echo "3. Add authorized redirect URI: http://localhost:8080/realms/gymnastics/broker/google/endpoint"
    echo "4. Copy the Client ID and Client Secret"
    exit 1
fi

GOOGLE_CLIENT_ID=$1
GOOGLE_CLIENT_SECRET=$2

echo "Configuring Google OAuth in Keycloak..."

# Login to Keycloak admin
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh config credentials \
    --server http://localhost:8080 \
    --realm master \
    --user admin \
    --password admin

# Create Google identity provider
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh create identity-provider/instances \
    -r gymnastics \
    -s alias=google \
    -s providerId=google \
    -s enabled=true \
    -s 'config.useJwksUrl="true"' \
    -s config.clientId="$GOOGLE_CLIENT_ID" \
    -s config.clientSecret="$GOOGLE_CLIENT_SECRET" \
    -s 'config.syncMode="IMPORT"' \
    -s 'config.guiOrder="1"' || echo "Google provider already exists, updating..."

# If creation failed (already exists), update instead
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update identity-provider/instances/google \
    -r gymnastics \
    -s enabled=true \
    -s config.clientId="$GOOGLE_CLIENT_ID" \
    -s config.clientSecret="$GOOGLE_CLIENT_SECRET" 2>/dev/null || true

# Add default mapper to set tenant_id attribute
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh create identity-provider/instances/google/mappers \
    -r gymnastics \
    -s name=google-tenant-id-mapper \
    -s identityProviderAlias=google \
    -s identityProviderMapper=hardcoded-attribute-idp-mapper \
    -s 'config."attribute.name"=tenant_id' \
    -s 'config."attribute.value"=default-tenant' 2>/dev/null || echo "Mapper already exists"

# Enable user registration for federated users
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh update realms/gymnastics \
    -s registrationAllowed=true

echo ""
echo "✅ Google OAuth configured successfully!"
echo ""
echo "Next steps:"
echo "1. Users can now click 'Sign in with Google' on the login page"
echo "2. First-time Google users will be automatically registered"
echo "3. They will be assigned to 'default-tenant' initially"
echo ""
echo "To test, visit: http://localhost:3001 or http://localhost:3002"
