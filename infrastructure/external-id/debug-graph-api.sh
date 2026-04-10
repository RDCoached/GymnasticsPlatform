#!/usr/bin/env bash
set -euo pipefail

# Load configuration
source external-id-config.env

echo "Getting access token..."
CIAM_GRAPH_TOKEN=$(az account get-access-token \
  --resource-type ms-graph \
  --tenant "$CIAM_TENANT_ID" \
  --query accessToken -o tsv)

if [ -z "$CIAM_GRAPH_TOKEN" ]; then
  echo "❌ Failed to get token"
  exit 1
fi

echo "✅ Got token (length: ${#CIAM_GRAPH_TOKEN})"
echo ""

echo "Querying for API application..."
echo "Client ID: $API_CLIENT_ID"
echo ""

API_APP_RESPONSE=$(curl -v -X GET \
  "https://graph.microsoft.com/v1.0/applications?\$filter=appId eq '$API_CLIENT_ID'" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" 2>&1)

echo ""
echo "Response:"
echo "$API_APP_RESPONSE"
echo ""

API_APP_ID=$(echo "$API_APP_RESPONSE" | jq -r '.value[0].id // empty' 2>&1)

echo "Parsed App ID: $API_APP_ID"
