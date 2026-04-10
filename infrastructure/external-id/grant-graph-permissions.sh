#!/usr/bin/env bash
set -euo pipefail

echo "🔐 Granting Microsoft Graph API permissions to API application"
echo "==============================================================="
echo ""

# Load configuration
if [ ! -f external-id-config.env ]; then
  echo "❌ external-id-config.env not found. Run setup-external-id.sh first."
  exit 1
fi

source external-id-config.env

echo "Configuration:"
echo "  CIAM Tenant ID: $CIAM_TENANT_ID"
echo "  API Client ID: $API_CLIENT_ID"
echo ""

# Get access token for Microsoft Graph API using Azure CLI (user-delegated)
echo "🔑 Getting Microsoft Graph API access token (user-delegated)..."
echo "You may be prompted to sign in to the CIAM tenant..."

# Set the account to the CIAM tenant
az account set --subscription "$CIAM_TENANT_ID" 2>/dev/null || true

# Get user-delegated token for Graph API
CIAM_GRAPH_TOKEN=$(az account get-access-token \
  --resource-type ms-graph \
  --tenant "$CIAM_TENANT_ID" \
  --query accessToken -o tsv)

if [ -z "$CIAM_GRAPH_TOKEN" ]; then
  echo "❌ Failed to get Microsoft Graph API access token"
  echo ""
  echo "Please ensure:"
  echo "  1. You are logged in to Azure CLI: az login"
  echo "  2. You have Global Administrator access to the CIAM tenant"
  echo "  3. The tenant ID is correct: $CIAM_TENANT_ID"
  exit 1
fi

echo "✅ Got Microsoft Graph API token"
echo ""

# Get the API application object ID
echo "📱 Finding API application..."
echo "   Querying for appId: $API_CLIENT_ID"

API_APP_RESPONSE=$(curl -s --max-time 30 -w "\nHTTP_STATUS:%{http_code}" \
  --get \
  --data-urlencode "\$filter=appId eq '$API_CLIENT_ID'" \
  "https://graph.microsoft.com/v1.0/applications" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN")

CURL_EXIT=$?
if [ $CURL_EXIT -ne 0 ]; then
  echo "❌ HTTP request failed (exit code: $CURL_EXIT)"
  exit 1
fi

# Extract HTTP status code
HTTP_STATUS=$(echo "$API_APP_RESPONSE" | grep "HTTP_STATUS:" | cut -d: -f2)
API_APP_RESPONSE=$(echo "$API_APP_RESPONSE" | sed '/HTTP_STATUS:/d')

echo "   HTTP Status: $HTTP_STATUS"

if [ "$HTTP_STATUS" != "200" ]; then
  echo "❌ Graph API returned HTTP $HTTP_STATUS"
  echo "Response:"
  echo "$API_APP_RESPONSE" | jq '.' 2>/dev/null || echo "$API_APP_RESPONSE"
  exit 1
fi

# Check for error in response
if echo "$API_APP_RESPONSE" | jq -e '.error' > /dev/null 2>&1; then
  echo "❌ Graph API returned error:"
  echo "$API_APP_RESPONSE" | jq '.error'
  exit 1
fi

API_APP_ID=$(echo "$API_APP_RESPONSE" | jq -r '.value[0].id // empty')

if [ -z "$API_APP_ID" ]; then
  echo "❌ Failed to find API application with client ID $API_CLIENT_ID"
  echo "Response was:"
  echo "$API_APP_RESPONSE" | jq '.' 2>/dev/null || echo "$API_APP_RESPONSE"
  exit 1
fi

echo "✅ Found API application (Object ID: $API_APP_ID)"
echo ""

# Get Microsoft Graph service principal ID
echo "🔍 Finding Microsoft Graph service principal..."
GRAPH_SP_RESPONSE=$(curl -s --max-time 30 \
  --get \
  --data-urlencode "\$filter=appId eq '00000003-0000-0000-c000-000000000000'" \
  "https://graph.microsoft.com/v1.0/servicePrincipals" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN")

if [ $? -ne 0 ]; then
  echo "❌ HTTP request failed or timed out"
  exit 1
fi

GRAPH_SP_ID=$(echo "$GRAPH_SP_RESPONSE" | jq -r '.value[0].id // empty')

if [ -z "$GRAPH_SP_ID" ]; then
  echo "❌ Failed to find Microsoft Graph service principal"
  exit 1
fi

echo "✅ Found Microsoft Graph service principal (ID: $GRAPH_SP_ID)"
echo ""

# Get User.ReadWrite.All permission ID (required to create users)
echo "🔍 Finding User.ReadWrite.All permission..."
PERMISSIONS_RESPONSE=$(curl -s --max-time 30 -X GET \
  "https://graph.microsoft.com/v1.0/servicePrincipals/$GRAPH_SP_ID" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN")

if [ $? -ne 0 ]; then
  echo "❌ HTTP request failed or timed out"
  exit 1
fi

USER_READWRITE_ALL_ID=$(echo "$PERMISSIONS_RESPONSE" | jq -r '.appRoles[] | select(.value=="User.ReadWrite.All") | .id // empty')

if [ -z "$USER_READWRITE_ALL_ID" ]; then
  echo "❌ Failed to find User.ReadWrite.All permission"
  exit 1
fi

echo "✅ Found User.ReadWrite.All permission (ID: $USER_READWRITE_ALL_ID)"
echo ""

# Add Microsoft Graph permissions to API application
echo "🔑 Adding Microsoft Graph API permissions to application..."
GRAPH_PERMISSIONS_RESPONSE=$(curl -s --max-time 30 -X PATCH \
  "https://graph.microsoft.com/v1.0/applications/$API_APP_ID" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"requiredResourceAccess\": [
      {
        \"resourceAppId\": \"00000003-0000-0000-c000-000000000000\",
        \"resourceAccess\": [
          {
            \"id\": \"$USER_READWRITE_ALL_ID\",
            \"type\": \"Role\"
          }
        ]
      }
    ]
  }")

if [ $? -ne 0 ]; then
  echo "❌ HTTP request failed or timed out"
  exit 1
fi

echo "✅ Microsoft Graph API permissions added to application manifest"
echo ""

# Get the API service principal
echo "📱 Finding API service principal..."
API_SP_ID=$(curl -s \
  --get \
  --data-urlencode "\$filter=appId eq '$API_CLIENT_ID'" \
  "https://graph.microsoft.com/v1.0/servicePrincipals" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  | jq -r '.value[0].id // empty')

if [ -z "$API_SP_ID" ]; then
  echo "❌ Failed to find API service principal"
  exit 1
fi

echo "✅ Found API service principal (ID: $API_SP_ID)"
echo ""

# Grant admin consent for Graph API permissions
echo "🔐 Granting admin consent for Graph API permissions..."
CONSENT_RESPONSE=$(curl -s -X POST \
  "https://graph.microsoft.com/v1.0/servicePrincipals/$API_SP_ID/appRoleAssignments" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"principalId\": \"$API_SP_ID\",
    \"resourceId\": \"$GRAPH_SP_ID\",
    \"appRoleId\": \"$USER_READWRITE_ALL_ID\"
  }")

CONSENT_ID=$(echo "$CONSENT_RESPONSE" | jq -r '.id // empty')

if [ -z "$CONSENT_ID" ]; then
  # Check if permission already exists
  EXISTING_CONSENT=$(curl -s -X GET \
    "https://graph.microsoft.com/v1.0/servicePrincipals/$API_SP_ID/appRoleAssignments" \
    -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
    | jq -r ".value[] | select(.appRoleId==\"$USER_READWRITE_ALL_ID\") | .id // empty")

  if [ -n "$EXISTING_CONSENT" ]; then
    echo "✅ Admin consent already granted (ID: $EXISTING_CONSENT)"
  else
    echo "❌ Failed to grant admin consent"
    echo "Response: $CONSENT_RESPONSE"
    exit 1
  fi
else
  echo "✅ Admin consent granted for Graph API permissions (ID: $CONSENT_ID)"
fi

echo ""
echo "✅ All Graph API permissions configured successfully!"
echo ""
echo "The API application can now:"
echo "  - Read user profiles via Microsoft Graph API"
echo "  - Check if emails exist in the tenant"
echo "  - Manage user accounts"
echo ""
echo "Next steps:"
echo "  1. Restart the API application"
echo "  2. Try registering a new user"
