#!/usr/bin/env bash
set -euo pipefail

echo "⚙️  Configuring applications with External ID settings"
echo "====================================================="
echo ""

# Load External ID configuration
if [ ! -f external-id-config.env ]; then
  echo "❌ external-id-config.env not found"
  echo "Run ./setup-external-id.sh first to create the External ID tenant"
  exit 1
fi

source external-id-config.env

echo "Loading configuration:"
echo "  Tenant ID: $CIAM_TENANT_ID"
echo "  Authority: $CIAM_AUTHORITY"
echo ""

# Step 1: Update backend user secrets
echo "🔧 Updating backend user secrets..."

cd ../../src/GymnasticsPlatform.Api

dotnet user-secrets set "Authentication:ExternalId:TenantId" "$CIAM_TENANT_ID"
dotnet user-secrets set "Authentication:ExternalId:ApiClientId" "$API_CLIENT_ID"
dotnet user-secrets set "Authentication:ExternalId:ApiClientSecret" "$API_CLIENT_SECRET"
dotnet user-secrets set "Authentication:ExternalId:Authority" "$CIAM_AUTHORITY"

echo "✅ Backend user secrets updated"
echo ""

# Step 2: Update frontend .env.local files
echo "🔧 Updating frontend configuration..."

cd ../../frontend/user-portal

cat > .env.local <<EOF
VITE_EXTERNAL_ID_TENANT_ID=$CIAM_TENANT_ID
VITE_EXTERNAL_ID_CLIENT_ID=$USER_PORTAL_CLIENT_ID
VITE_EXTERNAL_ID_AUTHORITY=$CIAM_AUTHORITY
VITE_API_CLIENT_ID=$API_CLIENT_ID
VITE_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_API_BASE_URL=http://localhost:5001
EOF

echo "✅ User portal .env.local updated"

cd ../admin-portal

cat > .env.local <<EOF
VITE_EXTERNAL_ID_TENANT_ID=$CIAM_TENANT_ID
VITE_EXTERNAL_ID_CLIENT_ID=$ADMIN_PORTAL_CLIENT_ID
VITE_EXTERNAL_ID_AUTHORITY=$CIAM_AUTHORITY
VITE_API_CLIENT_ID=$API_CLIENT_ID
VITE_REDIRECT_URI=http://localhost:3002/auth/callback
VITE_API_BASE_URL=http://localhost:5001
EOF

echo "✅ Admin portal .env.local updated"
echo ""

cd ../..

echo "✅ Configuration complete!"
echo ""
echo "Next steps:"
echo "  1. Restart the API: ./dev.sh api"
echo "  2. Restart the user portal: ./dev.sh user-portal"
echo "  3. Restart the admin portal: ./dev.sh admin-portal"
echo "  4. Test sign-in with Google OAuth or Microsoft account"
echo ""
echo "Sign-in options:"
echo "  - Google account (any @gmail.com)"
echo "  - Microsoft personal account (@live.com, @outlook.com, @hotmail.com)"
echo "  - Email/password (local accounts in External ID)"
