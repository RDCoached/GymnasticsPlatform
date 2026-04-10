#!/usr/bin/env bash
set -euo pipefail

source external-id-config.env

echo "Getting token..."
CIAM_GRAPH_TOKEN=$(az account get-access-token \
  --resource-type ms-graph \
  --tenant "$CIAM_TENANT_ID" \
  --query accessToken -o tsv)

echo "Token acquired (length: ${#CIAM_GRAPH_TOKEN})"
echo ""

echo "Testing Graph API call with verbose output..."
echo ""

curl -v --max-time 30 \
  "https://graph.microsoft.com/v1.0/applications?\$filter=appId eq '$API_CLIENT_ID'" \
  -H "Authorization: Bearer $CIAM_GRAPH_TOKEN" \
  2>&1 | head -50

echo ""
echo "Done"
