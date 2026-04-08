#!/usr/bin/env bash
set -euo pipefail

# Extract Terraform outputs and configure applications
echo "📋 Extracting Terraform outputs..." >&2

EXTERNAL_ID_TENANT_ID=$(terraform output -raw external_id_tenant_id)
API_CLIENT_ID=$(terraform output -raw api_client_id)
API_CLIENT_SECRET=$(terraform output -raw api_client_secret)
USER_PORTAL_CLIENT_ID=$(terraform output -raw user_portal_client_id)
ADMIN_PORTAL_CLIENT_ID=$(terraform output -raw admin_portal_client_id)
AUTHORITY_URL=$(terraform output -raw authority_url)

echo "✅ Configuration values extracted" >&2
echo "" >&2
echo "Backend (.NET user secrets):" >&2
dotnet user-secrets set 'Authentication:ExternalId:TenantId' "$EXTERNAL_ID_TENANT_ID" --project ../../src/GymnasticsPlatform.Api
dotnet user-secrets set 'Authentication:ExternalId:ApiClientId' "$API_CLIENT_ID" --project ../../src/GymnasticsPlatform.Api
dotnet user-secrets set 'Authentication:ExternalId:ApiClientSecret' "$API_CLIENT_SECRET" --project ../../src/GymnasticsPlatform.Api
dotnet user-secrets set 'Authentication:ExternalId:Authority' "$AUTHORITY_URL" --project ../../src/GymnasticsPlatform.Api

echo "" >&2
echo "Frontend (.env.local - User Portal):" >&2
echo "  VITE_EXTERNAL_ID_TENANT_ID=$EXTERNAL_ID_TENANT_ID" >&2
echo "  VITE_EXTERNAL_ID_CLIENT_ID=$USER_PORTAL_CLIENT_ID" >&2
echo "  VITE_EXTERNAL_ID_AUTHORITY=$AUTHORITY_URL" >&2
echo "  VITE_API_CLIENT_ID=$API_CLIENT_ID" >&2
echo "" >&2
echo "Frontend (.env.local - Admin Portal):" >&2
echo "  VITE_EXTERNAL_ID_TENANT_ID=$EXTERNAL_ID_TENANT_ID" >&2
echo "  VITE_EXTERNAL_ID_CLIENT_ID=$ADMIN_PORTAL_CLIENT_ID" >&2
echo "  VITE_EXTERNAL_ID_AUTHORITY=$AUTHORITY_URL" >&2
echo "  VITE_API_CLIENT_ID=$API_CLIENT_ID" >&2
