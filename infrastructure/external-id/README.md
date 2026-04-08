# External ID Infrastructure

## Overview

This Terraform module provisions the Azure AD (External ID/CIAM) infrastructure for the Gymnastics Platform, including:
- API app registration with OAuth2 scopes
- User Portal SPA registration
- Admin Portal SPA registration
- Service principals for all applications

**Note**: At the time of implementation (April 2026), Microsoft Entra External ID is still in preview. This configuration uses standard Azure AD app registrations which are compatible with External ID. When External ID becomes generally available, this configuration can be updated to use the External ID-specific Terraform provider.

## Prerequisites

- Azure subscription with appropriate permissions
- Terraform 1.5+
- Azure CLI installed and authenticated (`az login`)
- (Optional) Google Cloud Console account for Google OAuth

## Setup

### 1. Create Google OAuth Client (Optional)

If you want to support Google OAuth:

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Go to Credentials → Create Credentials → OAuth 2.0 Client ID
5. Application type: Web application
6. Authorized redirect URIs:
   - `http://localhost:5173/auth/callback` (user portal dev)
   - `http://localhost:3002/auth/callback` (admin portal dev)
   - Your production URLs
7. Copy Client ID and Client Secret

### 2. Configure Terraform

```bash
cd infrastructure/external-id

# Copy example file
cp terraform.tfvars.example terraform.tfvars

# Edit with your values
# Required: tenant_id (get from: az account show --query tenantId -o tsv)
# Optional: google_client_id, google_client_secret
nano terraform.tfvars
```

### 3. Initialize and Apply

```bash
# Initialize Terraform
terraform init

# Preview changes
terraform plan

# Apply configuration
terraform apply

# Extract outputs for configuration
./outputs.sh
```

### 4. Configure Application

The `outputs.sh` script displays all configuration needed for both backend and frontend.

#### Backend Configuration

Run these commands to set user secrets:

```bash
./outputs.sh | grep 'dotnet user-secrets' | bash
```

Or manually:

```bash
dotnet user-secrets set 'Authentication:ExternalId:TenantId' '<tenant-id>' --project ../../src/GymnasticsPlatform.Api
dotnet user-secrets set 'Authentication:ExternalId:ApiClientId' '<client-id>' --project ../../src/GymnasticsPlatform.Api
dotnet user-secrets set 'Authentication:ExternalId:ApiClientSecret' '<secret>' --project ../../src/GymnasticsPlatform.Api
dotnet user-secrets set 'Authentication:ExternalId:Authority' '<authority-url>' --project ../../src/GymnasticsPlatform.Api
```

#### Frontend Configuration

Create `.env.local` files:

**User Portal** (`frontend/user-portal/.env.local`):
```bash
VITE_EXTERNAL_ID_TENANT_ID=<tenant-id>
VITE_EXTERNAL_ID_CLIENT_ID=<user-portal-client-id>
VITE_EXTERNAL_ID_AUTHORITY=<authority-url>
VITE_API_CLIENT_ID=<api-client-id>
VITE_REDIRECT_URI=http://localhost:5173/auth/callback
```

**Admin Portal** (`frontend/admin-portal/.env.local`):
```bash
VITE_EXTERNAL_ID_TENANT_ID=<tenant-id>
VITE_EXTERNAL_ID_CLIENT_ID=<admin-portal-client-id>
VITE_EXTERNAL_ID_AUTHORITY=<authority-url>
VITE_API_CLIENT_ID=<api-client-id>
VITE_REDIRECT_URI=http://localhost:3002/auth/callback
VITE_API_BASE_URL=http://localhost:5001
```

## Idempotency

This Terraform configuration is idempotent. Re-running `terraform apply` will:
- Only create resources that don't exist
- Update resources that have changed
- Leave unchanged resources untouched

It's safe to run multiple times.

## Managing Changes

### Update Redirect URIs

Edit `main.tf`, update the `redirect_uris` arrays, then run:

```bash
terraform apply
```

### Rotate Client Secret

The API client secret is managed by Terraform. To rotate:

```bash
terraform taint azuread_application_password.api_secret
terraform apply
./outputs.sh  # Get new secret
# Update user secrets
```

### Destroy Infrastructure

⚠️ **Warning**: This will delete all app registrations and associated data.

```bash
terraform destroy
```

## Troubleshooting

### "Insufficient privileges" error

Ensure your Azure account has one of these roles:
- Global Administrator
- Application Administrator
- Cloud Application Administrator

### "Redirect URI already exists"

Check for existing app registrations in Azure Portal:
- Azure Portal → Entra ID → App registrations
- Delete conflicting apps or update Terraform to import them

### Google OAuth not working

1. Verify Google Cloud Console settings:
   - OAuth consent screen configured
   - Authorized redirect URIs match exactly
2. Check Terraform outputs match `.env.local` values
3. Clear browser cache/cookies

## Architecture Notes

### Why Not Azure AD B2C?

Azure AD B2C is being sunset in May 2025. External ID (CIAM) is the replacement. This configuration uses standard Azure AD app registrations which are forward-compatible with External ID.

### Social Identity Providers

This configuration provisions the Azure AD infrastructure. To add social providers:

1. **Google**: Configure in Azure Portal → Entra ID → External Identities → All identity providers
2. **Microsoft**: Built-in, works automatically with personal Microsoft accounts

Future versions of this Terraform module will provision social providers directly when the External ID provider is GA.

### Multi-Tenancy

The application uses a custom `tenant_id` attribute stored in user profiles, not Azure AD tenant IDs. The `tenant_id` here refers to the Azure AD tenant hosting the application, not application-level tenants.

## CI/CD Integration

Infrastructure is automatically provisioned on every push to `main`:

### GitHub Actions Workflow

The `.github/workflows/infrastructure.yml` workflow:
- ✅ Runs on pushes to main
- ✅ Provisions all Azure AD app registrations
- ✅ Configures Google identity provider
- ✅ Exports outputs for deployment

### Setup GitHub Secrets

Run once to configure CI/CD:

```bash
./setup-github-secrets.sh
```

This sets:
- `ARM_CLIENT_ID` - Service principal client ID
- `ARM_CLIENT_SECRET` - Service principal secret
- `ARM_TENANT_ID` - Azure tenant ID
- `ARM_SUBSCRIPTION_ID` - Azure subscription ID

### Triggering Infrastructure Updates

```bash
git add infrastructure/external-id/
git commit -m "feat: update External ID configuration"
git push origin main
```

The workflow automatically applies Terraform changes.

## Next Steps

After infrastructure is provisioned:

1. **Test locally**: Run `./outputs.sh` to configure local environment
2. **Deploy application**: Push to main triggers infrastructure + app deployment
3. **Monitor**: Check GitHub Actions for deployment status
4. **Update production URLs**: Add production redirect URIs to `terraform.tfvars`
