#!/bin/bash
set -e

# Azure Entra ID App Registration Setup Script (Idempotent)
# This script creates or updates all required app registrations for the Gymnastics Platform
# Can be run multiple times safely - will reuse existing apps if found
# Run with: ./setup-entra-apps.sh

echo "🚀 Setting up Azure Entra ID App Registrations for Gymnastics Platform"
echo ""

# Check if logged in to Azure
if ! az account show &> /dev/null; then
    echo "❌ Not logged in to Azure. Run 'az login' first."
    exit 1
fi

TENANT_ID=$(az account show --query tenantId -o tsv)
echo "✅ Using tenant: $TENANT_ID"
echo ""

# Helper function to get or create app registration
get_or_create_app() {
    local display_name="$1"
    local sign_in_audience="${2:-AzureADandPersonalMicrosoftAccount}"

    # Check if app already exists (case-insensitive search)
    # Search for apps with similar names to avoid duplicates
    local existing_app_id=$(az ad app list --query "[?contains(toLower(displayName), toLower('$display_name'))].appId | [0]" -o tsv 2>/dev/null)

    if [ -n "$existing_app_id" ] && [ "$existing_app_id" != "null" ]; then
        local existing_app_name=$(az ad app show --id "$existing_app_id" --query displayName -o tsv 2>/dev/null)
        echo "   ♻️  Found existing app: $existing_app_name" >&2
        echo "$existing_app_id"
    else
        echo "   ➕ Creating new app: $display_name" >&2
        az ad app create \
            --display-name "$display_name" \
            --sign-in-audience "$sign_in_audience" \
            --query appId -o tsv
    fi
}

# ============================================================================
# 1. Create or Update API App Registration
# ============================================================================
echo "📦 Setting up API App Registration..."

API_APP_NAME="Gymnastics Platform API"
API_APP_ID=$(get_or_create_app "$API_APP_NAME")

echo "   App ID: $API_APP_ID"

# Get Object ID (needed for extension attribute)
API_OBJECT_ID=$(az ad app show --id $API_APP_ID --query id -o tsv)
echo "   Object ID: $API_OBJECT_ID"

# Check if client secret exists, if not create one
SECRET_COUNT=$(az ad app show --id $API_APP_ID --query "passwordCredentials | length(@)" -o tsv 2>/dev/null || echo "0")

echo "   📊 Current client secrets: $SECRET_COUNT"

if [ "$SECRET_COUNT" -eq "0" ]; then
    echo "   🔑 Creating new client secret..."
    API_SECRET=$(az ad app credential reset \
        --id $API_APP_ID \
        --append \
        --display-name "API Graph Access Secret" \
        --years 2 \
        --query password -o tsv 2>/dev/null) || {
        echo "   ❌ Failed to create client secret. Please delete an existing secret in Azure Portal:"
        echo "      https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Credentials/appId/$API_APP_ID"
        echo "   Then run this script again."
        exit 1
    }
elif [ "$SECRET_COUNT" -ge "2" ]; then
    echo "   ⚠️  Maximum secrets reached ($SECRET_COUNT/2). Cannot create new secret."
    echo "   📋 To continue, delete an existing secret in Azure Portal:"
    echo "      https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Credentials/appId/$API_APP_ID"
    echo ""
    echo "   Or use Azure CLI:"
    echo "   az ad app credential list --id $API_APP_ID"
    echo "   az ad app credential delete --id $API_APP_ID --key-id <KEY_ID>"
    echo ""
    read -p "   Have you deleted a secret and want to create a new one? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "   🔑 Creating new client secret..."
        API_SECRET=$(az ad app credential reset \
            --id $API_APP_ID \
            --append \
            --display-name "API Graph Access Secret" \
            --years 2 \
            --query password -o tsv)
    else
        echo "   ⏭️  Skipping secret creation. You'll need to manually configure the secret."
        API_SECRET="<manually-configure-secret>"
    fi
else
    echo "   ⚠️  Client secret already exists. If you need a new one, delete an old one first."
    echo "   ⚠️  Using existing secret (cannot retrieve existing secrets via CLI)"
    API_SECRET="<existing-secret-not-retrievable>"
fi

echo "   ✅ API App configured"

# Add Microsoft Graph permissions (idempotent - won't fail if already exists)
echo "   🔐 Configuring Microsoft Graph API permissions..."

GRAPH_APP_ID="00000003-0000-0000-c000-000000000000"
USER_READWRITE_ALL_ID="741f803b-c850-494e-b5df-cde7c675a1ca"

# Check if permission already exists
EXISTING_PERMISSION=$(az ad app permission list --id $API_APP_ID \
    --query "[?resourceAppId=='$GRAPH_APP_ID'].resourceAccess[?id=='$USER_READWRITE_ALL_ID'].id" -o tsv 2>/dev/null || echo "")

if [ -z "$EXISTING_PERMISSION" ]; then
    echo "   ➕ Adding User.ReadWrite.All permission..."
    az ad app permission add \
        --id $API_APP_ID \
        --api $GRAPH_APP_ID \
        --api-permissions $USER_READWRITE_ALL_ID=Role
else
    echo "   ✓ User.ReadWrite.All permission already configured"
fi

# Grant admin consent
echo "   🔐 Granting admin consent..."
az ad app permission admin-consent --id $API_APP_ID 2>/dev/null || {
    echo "   ⚠️  Admin consent may require manual approval in Azure Portal"
    echo "   ⚠️  Go to: https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/CallAnAPI/appId/$API_APP_ID"
}

# Expose API scope
echo "   📡 Configuring API scope..."

API_URI="api://gymnastics-api"

# Check if identifier URI is already set
CURRENT_URI=$(az ad app show --id $API_APP_ID --query "identifierUris[0]" -o tsv 2>/dev/null || echo "")

if [ "$CURRENT_URI" != "$API_URI" ]; then
    echo "   ➕ Setting identifier URI: $API_URI"
    az ad app update --id $API_APP_ID --identifier-uris "$API_URI" 2>/dev/null || {
        echo "   ⚠️  Identifier URI may already be set"
    }
else
    echo "   ✓ Identifier URI already configured: $API_URI"
fi

# Check if scope exists
EXISTING_SCOPES=$(az ad app show --id $API_APP_ID --query "api.oauth2PermissionScopes[?value=='user.access'].id" -o tsv)

if [ -z "$EXISTING_SCOPES" ]; then
    echo "   ➕ Creating user.access scope..."
    SCOPE_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')

    cat > /tmp/api-scope.json <<EOF
{
    "oauth2PermissionScopes": [{
        "adminConsentDescription": "Allows access to the Gymnastics Platform API",
        "adminConsentDisplayName": "Access Gymnastics API",
        "id": "$SCOPE_ID",
        "isEnabled": true,
        "type": "User",
        "userConsentDescription": "Allows access to the Gymnastics Platform",
        "userConsentDisplayName": "Access Gymnastics API",
        "value": "user.access"
    }]
}
EOF

    az ad app update --id $API_APP_ID --set api=@/tmp/api-scope.json
    rm /tmp/api-scope.json
    echo "   ✅ API scope created: $API_URI/user.access"
else
    SCOPE_ID=$EXISTING_SCOPES
    echo "   ✓ API scope already configured: $API_URI/user.access"
fi

# Calculate extension app ID (object ID without hyphens)
EXTENSION_APP_ID=$(echo $API_OBJECT_ID | tr -d '-')
echo "   📝 Extension App ID: $EXTENSION_APP_ID"

# ============================================================================
# 2. Create or Update User Portal SPA App Registration
# ============================================================================
echo ""
echo "🌐 Setting up User Portal SPA App Registration..."

USER_PORTAL_APP_NAME="Gymnastics User Portal"
USER_PORTAL_APP_ID=$(get_or_create_app "$USER_PORTAL_APP_NAME")

echo "   App ID: $USER_PORTAL_APP_ID"

# Configure as SPA with redirect URIs
echo "   🔧 Configuring SPA settings..."

# Get current manifest
CURRENT_MANIFEST=$(az ad app show --id $USER_PORTAL_APP_ID)

# Update with SPA settings (logout URL requires HTTPS, so skip for dev)
echo "$CURRENT_MANIFEST" | jq '.spa.redirectUris = ["http://localhost:5173/auth/callback"] | .web.redirectUris = [] | .web.implicitGrantSettings.enableIdTokenIssuance = true | .web.implicitGrantSettings.enableAccessTokenIssuance = false' > /tmp/spa-manifest.json

az ad app update --id $USER_PORTAL_APP_ID --set @/tmp/spa-manifest.json 2>/dev/null || {
    echo "   ⚠️  Using fallback configuration method..."
    # Fallback: configure each property separately
    az rest --method PATCH \
        --url "https://graph.microsoft.com/v1.0/applications(appId='$USER_PORTAL_APP_ID')" \
        --headers "Content-Type=application/json" \
        --body "{\"spa\":{\"redirectUris\":[\"http://localhost:5173/auth/callback\"]},\"web\":{\"redirectUris\":[],\"implicitGrantSettings\":{\"enableIdTokenIssuance\":true,\"enableAccessTokenIssuance\":false}}}"
}

echo "   ℹ️  Note: Logout URL requires HTTPS. Set manually for production in Azure Portal."

rm -f /tmp/spa-manifest.json

# Add API permissions (delegated)
echo "   🔐 Configuring API permissions..."

# Check if permission already exists
EXISTING_API_PERMISSION=$(az ad app permission list --id $USER_PORTAL_APP_ID \
    --query "[?resourceAppId=='$API_APP_ID'].resourceAccess[?id=='$SCOPE_ID'].id" -o tsv 2>/dev/null || echo "")

if [ -z "$EXISTING_API_PERMISSION" ]; then
    echo "   ➕ Adding delegated permission to API..."
    # Wait for API app scope to be fully registered
    sleep 3
    az ad app permission add \
        --id $USER_PORTAL_APP_ID \
        --api $API_APP_ID \
        --api-permissions $SCOPE_ID=Scope
else
    echo "   ✓ API permission already configured"
fi

echo "   ✅ User Portal SPA configured"

# ============================================================================
# 3. Create or Update Admin Portal SPA App Registration
# ============================================================================
echo ""
echo "🔧 Setting up Admin Portal SPA App Registration..."

ADMIN_PORTAL_APP_NAME="Gymnastics Admin Portal"
ADMIN_PORTAL_APP_ID=$(get_or_create_app "$ADMIN_PORTAL_APP_NAME")

echo "   App ID: $ADMIN_PORTAL_APP_ID"

# Configure as SPA with redirect URIs
echo "   🔧 Configuring SPA settings..."

# Get current manifest
CURRENT_MANIFEST=$(az ad app show --id $ADMIN_PORTAL_APP_ID)

# Update with SPA settings (logout URL requires HTTPS, so skip for dev)
echo "$CURRENT_MANIFEST" | jq '.spa.redirectUris = ["http://localhost:3002/auth/callback"] | .web.redirectUris = [] | .web.implicitGrantSettings.enableIdTokenIssuance = true | .web.implicitGrantSettings.enableAccessTokenIssuance = false' > /tmp/spa-manifest-admin.json

az ad app update --id $ADMIN_PORTAL_APP_ID --set @/tmp/spa-manifest-admin.json 2>/dev/null || {
    echo "   ⚠️  Using fallback configuration method..."
    # Fallback: configure each property separately
    az rest --method PATCH \
        --url "https://graph.microsoft.com/v1.0/applications(appId='$ADMIN_PORTAL_APP_ID')" \
        --headers "Content-Type=application/json" \
        --body "{\"spa\":{\"redirectUris\":[\"http://localhost:3002/auth/callback\"]},\"web\":{\"redirectUris\":[],\"implicitGrantSettings\":{\"enableIdTokenIssuance\":true,\"enableAccessTokenIssuance\":false}}}"
}

echo "   ℹ️  Note: Logout URL requires HTTPS. Set manually for production in Azure Portal."

rm -f /tmp/spa-manifest-admin.json

# Add API permissions (delegated)
echo "   🔐 Configuring API permissions..."

EXISTING_API_PERMISSION=$(az ad app permission list --id $ADMIN_PORTAL_APP_ID \
    --query "[?resourceAppId=='$API_APP_ID'].resourceAccess[?id=='$SCOPE_ID'].id" -o tsv 2>/dev/null || echo "")

if [ -z "$EXISTING_API_PERMISSION" ]; then
    echo "   ➕ Adding delegated permission to API..."
    sleep 3
    az ad app permission add \
        --id $ADMIN_PORTAL_APP_ID \
        --api $API_APP_ID \
        --api-permissions $SCOPE_ID=Scope
else
    echo "   ✓ API permission already configured"
fi

echo "   ✅ Admin Portal SPA configured"

# ============================================================================
# 4. Output Summary
# ============================================================================
echo ""
echo "=========================================="
echo "✅ SETUP COMPLETE!"
echo "=========================================="
echo ""
echo "Save these values securely:"
echo ""
echo "TENANT_ID=$TENANT_ID"
echo "API_CLIENT_ID=$API_APP_ID"
echo "API_CLIENT_SECRET=$API_SECRET"
echo "EXTENSION_APP_ID=$EXTENSION_APP_ID"
echo "USER_PORTAL_CLIENT_ID=$USER_PORTAL_APP_ID"
echo "ADMIN_PORTAL_CLIENT_ID=$ADMIN_PORTAL_APP_ID"
echo ""
echo "Extension Attribute Name: extension_${EXTENSION_APP_ID}_tenant_id"
echo "API Scope: $API_URI/user.access"
echo ""

if [ "$API_SECRET" == "<existing-secret-not-retrievable>" ] || [ "$API_SECRET" == "<manually-configure-secret>" ]; then
    echo "⚠️  WARNING: Client secret not available from this script run."
    echo "   Options:"
    echo "   1. If you have the secret from a previous run, use that value"
    echo "   2. Delete existing secrets in Azure Portal and run this script again:"
    echo "      https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Credentials/appId/$API_APP_ID"
    echo ""
fi

echo "=========================================="
echo "Next Steps:"
echo "1. Save these values to .env or user secrets"
echo "2. Run ./configure-app.sh to configure your application"
echo "=========================================="
echo ""

# Optionally write to a .env file (NEVER commit this!)
if [ "$1" == "--write-env" ]; then
    if [ "$API_SECRET" == "<existing-secret-not-retrievable>" ] || [ "$API_SECRET" == "<manually-configure-secret>" ]; then
        echo "⚠️  Cannot write .env file - client secret not available"
        echo "   Options:"
        echo "   1. Use existing .azure-entra.env if you have one"
        echo "   2. Delete existing secrets in Azure Portal and run: ./setup-entra-apps.sh --write-env"
        echo "   3. Manually configure secrets using dotnet user-secrets"
        exit 1
    fi

    cat > .azure-entra.env <<EOF
# Azure Entra ID Configuration
# ⚠️ NEVER COMMIT THIS FILE TO GIT ⚠️
# Generated on $(date)

TENANT_ID=$TENANT_ID
API_CLIENT_ID=$API_APP_ID
API_CLIENT_SECRET=$API_SECRET
EXTENSION_APP_ID=$EXTENSION_APP_ID
USER_PORTAL_CLIENT_ID=$USER_PORTAL_APP_ID
ADMIN_PORTAL_CLIENT_ID=$ADMIN_PORTAL_APP_ID
EOF
    echo "📄 Configuration written to .azure-entra.env"
    echo "⚠️  Add .azure-entra.env to .gitignore immediately!"
fi
