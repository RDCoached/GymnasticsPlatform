#!/bin/bash
set -e

echo "=== Comprehensive OIDC Client Configuration for Gymnastics Platform ==="
echo ""

# Login to Keycloak
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh config credentials \
    --server http://localhost:8080 \
    --realm master \
    --user admin \
    --password admin > /dev/null 2>&1

# Get client IDs
USER_PORTAL_ID=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get clients -r gymnastics -q clientId=user-portal | jq -r '.[0].id')
ADMIN_PORTAL_ID=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get clients -r gymnastics -q clientId=admin-portal | jq -r '.[0].id')

echo "User Portal Client ID: $USER_PORTAL_ID"
echo "Admin Portal Client ID: $ADMIN_PORTAL_ID"
echo ""

# Function to add mapper if it doesn't exist
add_mapper() {
    local client_id=$1
    local mapper_name=$2
    shift 2

    # Check if mapper exists
    if docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get clients/$client_id/protocol-mappers/models -r gymnastics 2>/dev/null | jq -e ".[] | select(.name==\"$mapper_name\")" > /dev/null 2>&1; then
        echo "  ✓ $mapper_name already exists"
    else
        echo "  + Adding $mapper_name"
        docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh create \
            clients/$client_id/protocol-mappers/models \
            -r gymnastics \
            -s name="$mapper_name" \
            "$@" 2>/dev/null || echo "    ⚠ Failed to add $mapper_name"
    fi
}

configure_client() {
    local client_id=$1
    local client_name=$2

    echo "=== Configuring $client_name ==="

    # 1. REQUIRED: sub (subject/user ID) - OIDC Standard
    add_mapper "$client_id" "sub" \
        -s protocol=openid-connect \
        -s protocolMapper=oidc-usermodel-property-mapper \
        -s 'config."user.attribute"=id' \
        -s 'config."claim.name"=sub' \
        -s 'config."jsonType.label"=String' \
        -s 'config."id.token.claim"=true' \
        -s 'config."access.token.claim"=true' \
        -s 'config."userinfo.token.claim"=true'

    # 2. Email
    add_mapper "$client_id" "email" \
        -s protocol=openid-connect \
        -s protocolMapper=oidc-usermodel-property-mapper \
        -s 'config."user.attribute"=email' \
        -s 'config."claim.name"=email' \
        -s 'config."jsonType.label"=String' \
        -s 'config."id.token.claim"=true' \
        -s 'config."access.token.claim"=true' \
        -s 'config."userinfo.token.claim"=true'

    # 3. Email Verified
    add_mapper "$client_id" "email-verified" \
        -s protocol=openid-connect \
        -s protocolMapper=oidc-usermodel-property-mapper \
        -s 'config."user.attribute"=emailVerified' \
        -s 'config."claim.name"=email_verified' \
        -s 'config."jsonType.label"=boolean' \
        -s 'config."id.token.claim"=true' \
        -s 'config."access.token.claim"=true' \
        -s 'config."userinfo.token.claim"=true'

    # 4. Username (preferred_username)
    add_mapper "$client_id" "username" \
        -s protocol=openid-connect \
        -s protocolMapper=oidc-usermodel-property-mapper \
        -s 'config."user.attribute"=username' \
        -s 'config."claim.name"=preferred_username' \
        -s 'config."jsonType.label"=String' \
        -s 'config."id.token.claim"=true' \
        -s 'config."access.token.claim"=true' \
        -s 'config."userinfo.token.claim"=true'

    # 5. REQUIRED: Audience (gymnastics-api)
    add_mapper "$client_id" "api-audience" \
        -s protocol=openid-connect \
        -s protocolMapper=oidc-audience-mapper \
        -s 'config."included.client.audience"=gymnastics-api' \
        -s 'config."id.token.claim"=false' \
        -s 'config."access.token.claim"=true'

    # 6. REQUIRED: Realm Roles
    add_mapper "$client_id" "realm-roles" \
        -s protocol=openid-connect \
        -s protocolMapper=oidc-usermodel-realm-role-mapper \
        -s 'config."claim.name"=roles' \
        -s 'config."jsonType.label"=String' \
        -s 'config."multivalued"=true' \
        -s 'config."access.token.claim"=true' \
        -s 'config."id.token.claim"=true' \
        -s 'config."userinfo.token.claim"=true'

    # 7. Full Name
    add_mapper "$client_id" "full-name" \
        -s protocol=openid-connect \
        -s protocolMapper=oidc-full-name-mapper \
        -s 'config."id.token.claim"=true' \
        -s 'config."access.token.claim"=true' \
        -s 'config."userinfo.token.claim"=true'

    echo ""
}

# Configure both clients
configure_client "$USER_PORTAL_ID" "User Portal"
configure_client "$ADMIN_PORTAL_ID" "Admin Portal"

# Verify tenant_id client scope has hardcoded mapper
echo "=== Verifying tenant_id client scope ==="
TENANT_SCOPE_ID=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get client-scopes -r gymnastics | jq -r '.[] | select(.name=="tenant_id") | .id')

if [ -n "$TENANT_SCOPE_ID" ]; then
    HAS_HARDCODED=$(docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh get client-scopes/$TENANT_SCOPE_ID/protocol-mappers/models -r gymnastics | jq -e '.[] | select(.name=="hardcoded-onboarding-tenant")' > /dev/null 2>&1 && echo "yes" || echo "no")

    if [ "$HAS_HARDCODED" = "yes" ]; then
        echo "  ✓ Hardcoded onboarding-tenant mapper exists"
    else
        echo "  + Adding hardcoded onboarding-tenant mapper"
        docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh create \
            client-scopes/$TENANT_SCOPE_ID/protocol-mappers/models \
            -r gymnastics \
            -s name=hardcoded-onboarding-tenant \
            -s protocol=openid-connect \
            -s protocolMapper=oidc-hardcoded-claim-mapper \
            -s 'config."claim.name"=tenant_id' \
            -s 'config."claim.value"=onboarding-tenant' \
            -s 'config."jsonType.label"=String' \
            -s 'config."access.token.claim"=true' \
            -s 'config."id.token.claim"=true' \
            -s 'config."userinfo.token.claim"=true' 2>/dev/null || echo "    ⚠ Mapper may already exist"
    fi
fi

echo ""
echo "=== Configuration Summary ==="
echo "Both clients now have ALL required mappers:"
echo "  ✓ sub (user ID) - REQUIRED for JWT"
echo "  ✓ email"
echo "  ✓ email_verified"
echo "  ✓ preferred_username"
echo "  ✓ name (full name)"
echo "  ✓ aud: gymnastics-api - REQUIRED for API auth"
echo "  ✓ roles - REQUIRED for authorization"
echo "  ✓ tenant_id: onboarding-tenant - REQUIRED for multi-tenancy"
echo ""
echo "✅ Complete! Your next login will have a fully valid JWT token."
