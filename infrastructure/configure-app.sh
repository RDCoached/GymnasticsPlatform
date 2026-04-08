#!/bin/bash
set -e

# Configuration helper script
# Reads values from .azure-entra.env and configures both backend and frontend

if [ ! -f .azure-entra.env ]; then
    echo "❌ .azure-entra.env not found!"
    echo ""
    echo "Run this first: ./setup-entra-apps.sh --write-env"
    exit 1
fi

echo "📦 Loading Azure Entra ID configuration..."
source .azure-entra.env

# ============================================================================
# Configure Backend (using dotnet user-secrets)
# ============================================================================
echo ""
echo "🔧 Configuring backend API..."
cd ../src/GymnasticsPlatform.Api

# Initialize user secrets if not already done
dotnet user-secrets init > /dev/null 2>&1 || true

# Set all Entra ID configuration
dotnet user-secrets set "Authentication:EntraId:TenantId" "$TENANT_ID"
dotnet user-secrets set "Authentication:EntraId:ApiClientId" "$API_CLIENT_ID"
dotnet user-secrets set "Authentication:EntraId:ApiClientSecret" "$API_CLIENT_SECRET"
dotnet user-secrets set "Authentication:EntraId:ExtensionAppId" "$EXTENSION_APP_ID"
dotnet user-secrets set "Authentication:EntraId:Audience" "api://gymnastics-api"
dotnet user-secrets set "Authentication:EntraId:Instance" "https://login.microsoftonline.com/"

EXTENSION_ATTR="extension_${EXTENSION_APP_ID}_tenant_id"
dotnet user-secrets set "Authentication:EntraId:TenantIdExtensionAttributeName" "$EXTENSION_ATTR"

echo "   ✅ Backend configured with user secrets"

# ============================================================================
# Configure User Portal Frontend
# ============================================================================
echo ""
echo "🌐 Configuring User Portal frontend..."
cd ../../frontend/user-portal

cat > .env.local <<EOF
# User Portal - Azure Entra ID Configuration
# ⚠️ NEVER COMMIT THIS FILE TO GIT ⚠️
# Auto-generated on $(date)

VITE_ENTRA_CLIENT_ID=$USER_PORTAL_CLIENT_ID
VITE_ENTRA_TENANT_ID=$TENANT_ID
VITE_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_API_BASE_URL=http://localhost:5001
EOF

echo "   ✅ User Portal configured (.env.local created)"

# ============================================================================
# Configure Admin Portal Frontend
# ============================================================================
echo ""
echo "🔐 Configuring Admin Portal frontend..."
cd ../admin-portal

cat > .env.local <<EOF
# Admin Portal - Azure Entra ID Configuration
# ⚠️ NEVER COMMIT THIS FILE TO GIT ⚠️
# Auto-generated on $(date)

VITE_ENTRA_CLIENT_ID=$ADMIN_PORTAL_CLIENT_ID
VITE_ENTRA_TENANT_ID=$TENANT_ID
VITE_REDIRECT_URI=http://localhost:3002/auth/callback
VITE_API_BASE_URL=http://localhost:5001
EOF

echo "   ✅ Admin Portal configured (.env.local created)"

# ============================================================================
# Summary
# ============================================================================
cd ../../infrastructure
echo ""
echo "=========================================="
echo "✅ CONFIGURATION COMPLETE!"
echo "=========================================="
echo ""
echo "Backend API:"
echo "  - User secrets configured at:"
echo "    ~/.microsoft/usersecrets/<user-secrets-id>/secrets.json"
echo ""
echo "User Portal:"
echo "  - .env.local created at: frontend/user-portal/.env.local"
echo ""
echo "Admin Portal:"
echo "  - .env.local created at: frontend/admin-portal/.env.local"
echo ""
echo "Extension Attribute: $EXTENSION_ATTR"
echo "API Scope: api://gymnastics-api/user.access"
echo ""
echo "=========================================="
echo "Next Steps:"
echo "1. Verify configuration: dotnet user-secrets list --project ../src/GymnasticsPlatform.Api"
echo "2. Start the API: dotnet run --project ../src/GymnasticsPlatform.Api"
echo "3. Start User Portal: cd ../frontend/user-portal && npm run dev"
echo "4. Test authentication at http://localhost:5173"
echo "=========================================="
