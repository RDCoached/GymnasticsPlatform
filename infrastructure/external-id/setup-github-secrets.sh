#!/usr/bin/env bash
set -euo pipefail

echo "🔐 GitHub Secrets Setup for CI/CD Pipeline"
echo "==========================================="
echo ""
echo "The following secrets need to be added to your GitHub repository:"
echo ""
echo "Go to: https://github.com/YOUR_USERNAME/YOUR_REPO/settings/secrets/actions"
echo ""

if [ -f terraform.env ]; then
  source terraform.env

  echo "✅ Found terraform.env - here are your secret values:"
  echo ""
  echo "Secret Name: ARM_CLIENT_ID"
  echo "Value: $ARM_CLIENT_ID"
  echo ""
  echo "Secret Name: ARM_CLIENT_SECRET"
  echo "Value: $ARM_CLIENT_SECRET"
  echo ""
  echo "Secret Name: ARM_TENANT_ID"
  echo "Value: $ARM_TENANT_ID"
  echo ""
  echo "Secret Name: ARM_SUBSCRIPTION_ID"
  echo "Value: $ARM_SUBSCRIPTION_ID"
  echo ""

  # Offer to set them via gh CLI if available
  if command -v gh &> /dev/null; then
    echo "📝 GitHub CLI detected! Would you like to set these secrets automatically? (y/N)"
    read -r response
    if [[ "$response" =~ ^[Yy]$ ]]; then
      echo "Setting secrets..."
      gh secret set ARM_CLIENT_ID --body "$ARM_CLIENT_ID"
      gh secret set ARM_CLIENT_SECRET --body "$ARM_CLIENT_SECRET"
      gh secret set ARM_TENANT_ID --body "$ARM_TENANT_ID"
      gh secret set ARM_SUBSCRIPTION_ID --body "$ARM_SUBSCRIPTION_ID"
      echo "✅ All secrets set!"
    else
      echo "📋 Copy the values above and add them manually to GitHub."
    fi
  else
    echo "💡 Tip: Install GitHub CLI (gh) to set secrets automatically:"
    echo "   brew install gh"
    echo ""
    echo "📋 For now, copy the values above and add them manually to:"
    echo "   Repository Settings → Secrets and variables → Actions → New repository secret"
  fi
else
  echo "❌ terraform.env not found!"
  echo ""
  echo "Run ./setup-terraform-sp.sh first to create the service principal and credentials."
  exit 1
fi
