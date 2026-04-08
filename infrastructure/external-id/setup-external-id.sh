#!/usr/bin/env bash
set -euo pipefail

echo "🔐 Setting up Microsoft Entra External ID (CIAM)"
echo "================================================"
echo ""

# Load environment variables if terraform.env exists
if [ -f terraform.env ]; then
  source terraform.env
fi

# Required variables
SUBSCRIPTION_ID="${ARM_SUBSCRIPTION_ID:?ARM_SUBSCRIPTION_ID not set. Run setup-terraform-sp.sh first.}"
TENANT_ID="${ARM_TENANT_ID:?ARM_TENANT_ID not set. Run setup-terraform-sp.sh first.}"
CLIENT_ID="${ARM_CLIENT_ID:?ARM_CLIENT_ID not set. Run setup-terraform-sp.sh first.}"
CLIENT_SECRET="${ARM_CLIENT_SECRET:?ARM_CLIENT_SECRET not set. Run setup-terraform-sp.sh first.}"

# Check if tfvars exists for Google credentials
if [ ! -f terraform.tfvars ]; then
  echo "❌ terraform.tfvars not found. Please create it from terraform.tfvars.example"
  exit 1
fi

# Extract Google credentials from tfvars
GOOGLE_CLIENT_ID=$(grep '^google_client_id' terraform.tfvars | cut -d'"' -f2)
GOOGLE_CLIENT_SECRET=$(grep '^google_client_secret' terraform.tfvars | cut -d'"' -f2)

if [ -z "$GOOGLE_CLIENT_ID" ] || [ -z "$GOOGLE_CLIENT_SECRET" ]; then
  echo "❌ Google OAuth credentials not found in terraform.tfvars"
  exit 1
fi

# Configuration
CIAM_TENANT_NAME="${CIAM_TENANT_NAME:-gymnastics-ciam}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-gymnastics-ciam}"
LOCATION="${LOCATION:-unitedstates}"
API_VERSION="2023-05-17-preview"

echo "Configuration:"
echo "  Tenant Name: $CIAM_TENANT_NAME"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  Subscription: $SUBSCRIPTION_ID"
echo ""

# Get access token for Azure REST API
echo "🔑 Getting Azure REST API access token..."
ARM_TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=https://management.azure.com/.default" \
  -d "grant_type=client_credentials" \
  | jq -r '.access_token')

if [ -z "$ARM_TOKEN" ] || [ "$ARM_TOKEN" = "null" ]; then
  echo "❌ Failed to get Azure REST API access token"
  exit 1
fi

echo "✅ Got Azure REST API token"

# Get access token for Microsoft Graph API
echo "🔑 Getting Microsoft Graph API access token..."
GRAPH_TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=https://graph.microsoft.com/.default" \
  -d "grant_type=client_credentials" \
  | jq -r '.access_token')

if [ -z "$GRAPH_TOKEN" ] || [ "$GRAPH_TOKEN" = "null" ]; then
  echo "❌ Failed to get Microsoft Graph API access token"
  exit 1
fi

echo "✅ Got Microsoft Graph API token"
echo ""

# Step 1: Create Resource Group if it doesn't exist
echo "📦 Checking resource group..."
RG_EXISTS=$(curl -s -X GET \
  "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP?api-version=2021-04-01" \
  -H "Authorization: Bearer $ARM_TOKEN" \
  | jq -r '.properties.provisioningState // "NotFound"')

if [ "$RG_EXISTS" = "NotFound" ]; then
  echo "Creating resource group $RESOURCE_GROUP..."
  curl -s -X PUT \
    "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP?api-version=2021-04-01" \
    -H "Authorization: Bearer $ARM_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"location\": \"$LOCATION\"
    }" > /dev/null
  echo "✅ Resource group created"
else
  echo "✅ Resource group already exists"
fi

echo ""

# Step 2: Create External ID (CIAM) Tenant
echo "🏢 Creating External ID (CIAM) tenant..."
TENANT_RESOURCE_NAME="ciam-$CIAM_TENANT_NAME"

CIAM_TENANT_RESPONSE=$(curl -s -X PUT \
  "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.AzureActiveDirectory/ciamDirectories/$TENANT_RESOURCE_NAME?api-version=$API_VERSION" \
  -H "Authorization: Bearer $ARM_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"location\": \"$LOCATION\",
    \"sku\": {
      \"name\": \"Standard\",
      \"tier\": \"A0\"
    },
    \"properties\": {
      \"createTenantProperties\": {
        \"displayName\": \"Gymnastics Platform CIAM\",
        \"countryCode\": \"US\"
      }
    }
  }")

echo "$CIAM_TENANT_RESPONSE" | jq '.'

# Extract tenant ID from response
CIAM_TENANT_ID=$(echo "$CIAM_TENANT_RESPONSE" | jq -r '.properties.tenantId // empty')
CIAM_DOMAIN=$(echo "$CIAM_TENANT_RESPONSE" | jq -r '.properties.domainName // empty')

if [ -z "$CIAM_TENANT_ID" ]; then
  echo "⚠️  Tenant creation may be in progress or already exists. Checking existing tenant..."

  # Try to get existing tenant
  EXISTING_TENANT=$(curl -s -X GET \
    "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.AzureActiveDirectory/ciamDirectories/$TENANT_RESOURCE_NAME?api-version=$API_VERSION" \
    -H "Authorization: Bearer $ARM_TOKEN")

  CIAM_TENANT_ID=$(echo "$EXISTING_TENANT" | jq -r '.properties.tenantId // empty')
  CIAM_DOMAIN=$(echo "$EXISTING_TENANT" | jq -r '.properties.domainName // empty')

  if [ -z "$CIAM_TENANT_ID" ]; then
    echo "❌ Failed to create or retrieve CIAM tenant"
    echo "Response: $CIAM_TENANT_RESPONSE"
    exit 1
  fi

  echo "✅ Found existing CIAM tenant"
else
  echo "✅ CIAM tenant created successfully"
fi

echo ""
echo "External ID Tenant Information:"
echo "  Tenant ID: $CIAM_TENANT_ID"
echo "  Domain: $CIAM_DOMAIN"
echo "  Authority: https://$CIAM_DOMAIN"
echo ""

# Wait a moment for tenant to be fully provisioned
echo "⏳ Waiting for tenant provisioning to complete..."
sleep 30

# Get new Graph token for the CIAM tenant
echo "🔑 Getting Graph token for CIAM tenant..."
CIAM_GRAPH_TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/$CIAM_TENANT_ID/oauth2/v2.0/token" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=https://graph.microsoft.com/.default" \
  -d "grant_type=client_credentials" \
  | jq -r '.access_token // empty')

if [ -z "$CIAM_GRAPH_TOKEN" ]; then
  echo "⚠️  Could not get token for new CIAM tenant yet. Service principal may need to be added."
  echo "You may need to:"
  echo "  1. Sign in to the CIAM tenant at https://entra.microsoft.com"
  echo "  2. Add the service principal $CLIENT_ID as an admin"
  echo "  3. Re-run this script"
  echo ""
  echo "For now, saving tenant information..."

  # Save what we have
  cat > ciam-tenant-info.txt <<EOF
CIAM_TENANT_ID=$CIAM_TENANT_ID
CIAM_DOMAIN=$CIAM_DOMAIN
CIAM_AUTHORITY=https://$CIAM_DOMAIN

Next steps:
1. Sign in to https://entra.microsoft.com with a Global Administrator account
2. Switch to the new tenant: $CIAM_DOMAIN
3. Add service principal $CLIENT_ID as an Application Administrator
4. Re-run this script to complete app registration setup
EOF

  echo "ℹ️  Tenant info saved to ciam-tenant-info.txt"
  exit 0
fi

echo "✅ Got Graph token for CIAM tenant"
echo ""

# Step 3: Register API Application
echo "📱 Registering API application..."

API_APP_RESPONSE=$(curl -s -X POST \
  "https://graph.microsoft.com/v1.0/applications" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"displayName\": \"Gymnastics Platform API\",
    \"signInAudience\": \"AzureADandPersonalMicrosoftAccount\",
    \"api\": {
      \"requestedAccessTokenVersion\": 2,
      \"oauth2PermissionScopes\": [
        {
          \"id\": \"00000000-0000-0000-0000-000000000001\",
          \"adminConsentDescription\": \"Allows access to the Gymnastics Platform API\",
          \"adminConsentDisplayName\": \"Access Gymnastics API\",
          \"userConsentDescription\": \"Allows access to the Gymnastics Platform\",
          \"userConsentDisplayName\": \"Access Gymnastics API\",
          \"type\": \"User\",
          \"value\": \"user.access\"
        }
      ]
    },
    \"identifierUris\": [\"api://$CIAM_TENANT_ID/gymnastics-api\"]
  }")

API_APP_ID=$(echo "$API_APP_RESPONSE" | jq -r '.id // empty')
API_CLIENT_ID=$(echo "$API_APP_RESPONSE" | jq -r '.appId // empty')

if [ -z "$API_APP_ID" ]; then
  echo "❌ Failed to create API application"
  echo "Response: $API_APP_RESPONSE"
  exit 1
fi

echo "✅ API application created"
echo "  App ID: $API_APP_ID"
echo "  Client ID: $API_CLIENT_ID"
echo ""

# Create service principal for API
echo "Creating service principal for API..."
curl -s -X POST \
  "https://graph.microsoft.com/v1.0/servicePrincipals" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"appId\": \"$API_CLIENT_ID\"
  }" > /dev/null

echo "✅ Service principal created"
echo ""

# Create API client secret
echo "🔐 Creating API client secret..."
API_SECRET_RESPONSE=$(curl -s -X POST \
  "https://graph.microsoft.com/v1.0/applications/$API_APP_ID/addPassword" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"passwordCredential\": {
      \"displayName\": \"API Graph Access Secret\"
    }
  }")

API_CLIENT_SECRET=$(echo "$API_SECRET_RESPONSE" | jq -r '.secretText // empty')

if [ -z "$API_CLIENT_SECRET" ]; then
  echo "❌ Failed to create API client secret"
  exit 1
fi

echo "✅ API client secret created"
echo ""

# Step 4: Register User Portal Application
echo "📱 Registering User Portal application..."

USER_PORTAL_RESPONSE=$(curl -s -X POST \
  "https://graph.microsoft.com/v1.0/applications" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"displayName\": \"Gymnastics User Portal\",
    \"signInAudience\": \"AzureADandPersonalMicrosoftAccount\",
    \"api\": {
      \"requestedAccessTokenVersion\": 2
    },
    \"spa\": {
      \"redirectUris\": [
        \"http://localhost:5173/auth/callback\"
      ]
    },
    \"requiredResourceAccess\": [
      {
        \"resourceAppId\": \"$API_CLIENT_ID\",
        \"resourceAccess\": [
          {
            \"id\": \"00000000-0000-0000-0000-000000000001\",
            \"type\": \"Scope\"
          }
        ]
      }
    ]
  }")

USER_PORTAL_APP_ID=$(echo "$USER_PORTAL_RESPONSE" | jq -r '.id // empty')
USER_PORTAL_CLIENT_ID=$(echo "$USER_PORTAL_RESPONSE" | jq -r '.appId // empty')

if [ -z "$USER_PORTAL_APP_ID" ]; then
  echo "❌ Failed to create User Portal application"
  echo "Response: $USER_PORTAL_RESPONSE"
  exit 1
fi

echo "✅ User Portal application created"
echo "  Client ID: $USER_PORTAL_CLIENT_ID"
echo ""

# Create service principal for User Portal
curl -s -X POST \
  "https://graph.microsoft.com/v1.0/servicePrincipals" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"appId\": \"$USER_PORTAL_CLIENT_ID\"
  }" > /dev/null

# Step 5: Register Admin Portal Application
echo "📱 Registering Admin Portal application..."

ADMIN_PORTAL_RESPONSE=$(curl -s -X POST \
  "https://graph.microsoft.com/v1.0/applications" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"displayName\": \"Gymnastics Admin Portal\",
    \"signInAudience\": \"AzureADandPersonalMicrosoftAccount\",
    \"api\": {
      \"requestedAccessTokenVersion\": 2
    },
    \"spa\": {
      \"redirectUris\": [
        \"http://localhost:3002/auth/callback\"
      ]
    },
    \"requiredResourceAccess\": [
      {
        \"resourceAppId\": \"$API_CLIENT_ID\",
        \"resourceAccess\": [
          {
            \"id\": \"00000000-0000-0000-0000-000000000001\",
            \"type\": \"Scope\"
          }
        ]
      }
    ]
  }")

ADMIN_PORTAL_APP_ID=$(echo "$ADMIN_PORTAL_RESPONSE" | jq -r '.id // empty')
ADMIN_PORTAL_CLIENT_ID=$(echo "$ADMIN_PORTAL_RESPONSE" | jq -r '.appId // empty')

if [ -z "$ADMIN_PORTAL_APP_ID" ]; then
  echo "❌ Failed to create Admin Portal application"
  echo "Response: $ADMIN_PORTAL_RESPONSE"
  exit 1
fi

echo "✅ Admin Portal application created"
echo "  Client ID: $ADMIN_PORTAL_CLIENT_ID"
echo ""

# Create service principal for Admin Portal
curl -s -X POST \
  "https://graph.microsoft.com/v1.0/servicePrincipals" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"appId\": \"$ADMIN_PORTAL_CLIENT_ID\"
  }" > /dev/null

# Step 6: Configure Google Identity Provider
echo "🔑 Configuring Google identity provider..."

GOOGLE_IDP_RESPONSE=$(curl -s -X POST \
  "https://graph.microsoft.com/beta/identity/identityProviders" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"@odata.type\": \"microsoft.graph.socialIdentityProvider\",
    \"type\": \"Google\",
    \"name\": \"Google\",
    \"clientId\": \"$GOOGLE_CLIENT_ID\",
    \"clientSecret\": \"$GOOGLE_CLIENT_SECRET\"
  }")

GOOGLE_IDP_ID=$(echo "$GOOGLE_IDP_RESPONSE" | jq -r '.id // empty')

if [ -z "$GOOGLE_IDP_ID" ]; then
  # Check if it already exists
  EXISTING_IDP=$(curl -s -X GET \
    "https://graph.microsoft.com/beta/identity/identityProviders" \
    -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
    | jq -r '.value[] | select(.type=="Google") | .id // empty')

  if [ -n "$EXISTING_IDP" ]; then
    echo "✅ Google identity provider already configured"
    GOOGLE_IDP_ID="$EXISTING_IDP"
  else
    echo "⚠️  Failed to create Google identity provider"
    echo "Response: $GOOGLE_IDP_RESPONSE"
  fi
else
  echo "✅ Google identity provider configured"
fi

echo ""

# Step 7: Output configuration
echo "✅ External ID setup complete!"
echo ""
echo "================================================"
echo "Configuration Values"
echo "================================================"
echo ""
echo "External ID Tenant:"
echo "  Tenant ID: $CIAM_TENANT_ID"
echo "  Domain: $CIAM_DOMAIN"
echo "  Authority: https://$CIAM_DOMAIN"
echo ""
echo "API Application:"
echo "  Client ID: $API_CLIENT_ID"
echo "  Client Secret: $API_CLIENT_SECRET"
echo ""
echo "User Portal:"
echo "  Client ID: $USER_PORTAL_CLIENT_ID"
echo ""
echo "Admin Portal:"
echo "  Client ID: $ADMIN_PORTAL_CLIENT_ID"
echo ""
echo "Google IDP:"
echo "  IDP ID: ${GOOGLE_IDP_ID:-Not configured}"
echo ""

# Save to file
cat > external-id-config.env <<EOF
# External ID (CIAM) Configuration
CIAM_TENANT_ID=$CIAM_TENANT_ID
CIAM_DOMAIN=$CIAM_DOMAIN
CIAM_AUTHORITY=https://$CIAM_DOMAIN

# API Application
API_CLIENT_ID=$API_CLIENT_ID
API_CLIENT_SECRET=$API_CLIENT_SECRET

# User Portal
USER_PORTAL_CLIENT_ID=$USER_PORTAL_CLIENT_ID

# Admin Portal
ADMIN_PORTAL_CLIENT_ID=$ADMIN_PORTAL_CLIENT_ID

# Google Identity Provider
GOOGLE_IDP_ID=${GOOGLE_IDP_ID:-}
EOF

echo "💾 Configuration saved to external-id-config.env"
echo ""
echo "Next steps:"
echo "  1. Run ./configure-apps.sh to update .env files and user secrets"
echo "  2. Restart the API and frontend applications"
echo "  3. Test Google OAuth and Microsoft account sign-in"
