#!/usr/bin/env bash
set -euo pipefail

echo "🔧 Creating Terraform service principal with required permissions..."

# Create app registration for Terraform
APP_NAME="Terraform-GymnasticsPlatform"
echo "Creating app registration: $APP_NAME"

APP_ID=$(az ad app create \
  --display-name "$APP_NAME" \
  --query appId -o tsv)

echo "✅ App created: $APP_ID"

# Create service principal
SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)
echo "✅ Service principal created: $SP_ID"

# Create client secret
SECRET=$(az ad app credential reset \
  --id "$APP_ID" \
  --append \
  --query password -o tsv)

echo "✅ Client secret created"

# Get tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)

# Grant Microsoft Graph permissions
echo "Granting Microsoft Graph API permissions..."

# Get Microsoft Graph app ID (well-known)
GRAPH_APP_ID="00000003-0000-0000-c000-000000000000"

# Get the IdentityProvider.ReadWrite.All permission ID
IDP_PERMISSION_ID=$(az ad sp show --id $GRAPH_APP_ID \
  --query "appRoles[?value=='IdentityProvider.ReadWrite.All'].id" -o tsv)

# Grant the permission
az ad app permission add \
  --id "$APP_ID" \
  --api "$GRAPH_APP_ID" \
  --api-permissions "$IDP_PERMISSION_ID=Role"

echo "⏳ Granting admin consent (requires admin privileges)..."
az ad app permission admin-consent --id "$APP_ID" || {
  echo "⚠️  Admin consent failed. You may need Global Administrator role."
  echo "   Manually grant consent in Azure Portal:"
  echo "   https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/CallAnAPI/appId/$APP_ID"
}

echo ""
echo "✅ Setup complete!"
echo ""
echo "📝 Add these to your environment or use for Terraform authentication:"
echo ""
echo "export ARM_CLIENT_ID=\"$APP_ID\""
echo "export ARM_CLIENT_SECRET=\"$SECRET\""
echo "export ARM_TENANT_ID=\"$TENANT_ID\""
echo "export ARM_SUBSCRIPTION_ID=\"$(az account show --query id -o tsv)\""
echo ""
echo "Or save to terraform.env:"
cat > terraform.env << EOF
ARM_CLIENT_ID=$APP_ID
ARM_CLIENT_SECRET=$SECRET
ARM_TENANT_ID=$TENANT_ID
ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
EOF

echo "✅ Credentials saved to terraform.env"
echo ""
echo "To use: source terraform.env && terraform apply"
