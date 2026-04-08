# Azure Entra ID Infrastructure Setup

This directory contains Infrastructure as Code (IaC) for setting up Azure Entra ID app registrations.

## Prerequisites

- Azure CLI installed: `brew install azure-cli`
- Logged in to Azure: `az login`
- Permissions to create app registrations in your Azure AD tenant

## Option 1: Bash Script (Recommended for Quick Setup)

The bash script uses Azure CLI to create all required app registrations.

```bash
# Make the script executable
chmod +x setup-entra-apps.sh

# Run the script
./setup-entra-apps.sh

# Optionally write output to .env file (NEVER commit this!)
./setup-entra-apps.sh --write-env
```

The script will:
1. Create API app registration with Graph API permissions
2. Create User Portal SPA app registration
3. Create Admin Portal SPA app registration
4. Grant admin consent for API permissions
5. Output all required IDs and secrets

**Important**: Save the output securely. The client secret cannot be retrieved later!

## After Setup

Once you've run the script, you'll get output like:

```
TENANT_ID=12345678-1234-1234-1234-123456789012
API_CLIENT_ID=87654321-4321-4321-4321-210987654321
API_CLIENT_SECRET=xxx~secret~xxx
EXTENSION_APP_ID=abc123def456...
USER_PORTAL_CLIENT_ID=11111111-1111-1111-1111-111111111111
ADMIN_PORTAL_CLIENT_ID=22222222-2222-2222-2222-222222222222
```

**Next steps**:

1. Run the configuration script:
   ```bash
   ./configure-app.sh
   ```

2. **NEVER commit secrets to git**. Add to `.gitignore`:
   ```
   .azure-entra.env
   **/.env.local
   ```

## Cleanup

To delete all created app registrations:

```bash
# List all apps (filter by name)
az ad app list --display-name "Gymnastics" --query "[].{Name:displayName, AppId:appId}" -o table

# Delete by App ID
az ad app delete --id <APP_ID>
```

## Production Setup

For production deployments:

1. Update redirect URIs in the script to match your production domain
2. Consider using Azure Key Vault for secret storage
3. Set up managed identities for the API to access Graph API (eliminates client secret)
4. Enable Conditional Access policies for additional security

## Troubleshooting

**Admin consent required**: If you see "admin consent required" when testing, run:
```bash
az ad app permission admin-consent --id <API_APP_ID>
```

**Extension attribute not working**: Verify the Extension App ID is the Object ID without hyphens:
```bash
az ad app show --id <API_APP_ID> --query id -o tsv | tr -d '-'
```

**CORS errors**: Add your frontend origins to the CORS configuration in `appsettings.json`
