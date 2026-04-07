# Entra ID Configuration Values Template

Use this template to track all configuration values as you set up Entra ID.

## Global Values

```
Azure Tenant ID: _______________________________________________
Azure Subscription ID: _________________________________________
```

## API App Registration

```
App Name: gymnastics-api
Application (client) ID: _______________________________________
Object ID: _____________________________________________________
Client Secret Value: ___________________________________________
Client Secret ID: ______________________________________________
Secret Expiration Date: ________________________________________

Extension Attribute App ID (without dashes): ___________________
Extension Attribute Name: extension_<appid>_tenant_id
Full Extension Attribute: ______________________________________

Application ID URI: api://gymnastics-api
API Scope: api://gymnastics-api/user.access

App Role Name: platform_admin
App Role ID: ___________________________________________________
```

## User Portal SPA Registration

```
App Name: gymnastics-user-portal
Application (client) ID: _______________________________________
Object ID: _____________________________________________________

Redirect URIs:
  - Development: http://localhost:5173/auth/callback
  - Production: https://app.gymnastics.example.com/auth/callback

Logout URL: https://app.gymnastics.example.com/sign-in
```

## Admin Portal SPA Registration

```
App Name: gymnastics-admin-portal
Application (client) ID: _______________________________________
Object ID: _____________________________________________________

Redirect URIs:
  - Development: http://localhost:5174/auth/callback
  - Production: https://admin.gymnastics.example.com/auth/callback

Logout URL: https://admin.gymnastics.example.com/sign-in
```

## Google Identity Provider

```
Google Cloud Project: __________________________________________
Google OAuth Client ID: ________________________________________
Google OAuth Client Secret: ____________________________________

Entra Identity Provider Name: Google
Domain Hint: google
```

---

## Backend Configuration (appsettings.json)

```json
{
  "Authentication": {
    "ActiveProvider": "Keycloak",
    "EntraId": {
      "TenantId": "_______________________________________________",
      "ApiClientId": "_________________________________________",
      "ApiClientSecret": "stored-in-keyvault",
      "Instance": "https://login.microsoftonline.com/",
      "Audience": "api://gymnastics-api",
      "ExtensionAppId": "_______________________________________",
      "TenantIdExtensionAttributeName": "extension_<appid>_tenant_id"
    }
  }
}
```

## User Portal Frontend (.env)

```bash
VITE_AUTH_PROVIDER=keycloak

# Entra ID
VITE_ENTRA_CLIENT_ID=_______________________________________
VITE_ENTRA_TENANT_ID=_______________________________________
VITE_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_API_SCOPE=api://gymnastics-api/user.access
```

## Admin Portal Frontend (.env)

```bash
VITE_AUTH_PROVIDER=keycloak

# Entra ID
VITE_ENTRA_CLIENT_ID=_______________________________________
VITE_ENTRA_TENANT_ID=_______________________________________
VITE_REDIRECT_URI=http://localhost:5174/auth/callback
VITE_API_SCOPE=api://gymnastics-api/user.access
```

---

## Azure Key Vault Secrets

Store these secrets in Azure Key Vault:

```
Secret Name: EntraIdApiClientSecret
Secret Value: _______________________________________________

Secret Name: GoogleOAuthClientSecret
Secret Value: _______________________________________________
```

Reference in appsettings:

```json
{
  "Authentication": {
    "EntraId": {
      "ApiClientSecret": "@Microsoft.KeyVault(SecretUri=https://<vault-name>.vault.azure.net/secrets/EntraIdApiClientSecret/)"
    }
  }
}
```

---

## Verification Checklist

### Azure Portal Setup
- [ ] API app registered
- [ ] Client secret created and stored
- [ ] Graph permissions granted (admin consent)
- [ ] Extension attribute created (`tenant_id`)
- [ ] App role created (`platform_admin`)
- [ ] Optional claims configured (email, name, extension_tenant_id)
- [ ] API exposed with `user.access` scope
- [ ] User Portal SPA registered
- [ ] User Portal redirect URIs configured
- [ ] User Portal permissions granted
- [ ] Admin Portal SPA registered
- [ ] Admin Portal redirect URIs configured
- [ ] Admin Portal permissions granted
- [ ] Google identity provider configured

### Configuration Files
- [ ] `appsettings.json` updated with Entra values
- [ ] User Portal `.env` created with Entra values
- [ ] Admin Portal `.env` created with Entra values
- [ ] Secrets stored in Azure Key Vault
- [ ] Key Vault references configured

### Testing
- [ ] Can acquire token using API app (client credentials)
- [ ] Can acquire token using User Portal (auth code + PKCE)
- [ ] Extension attribute appears in token claims
- [ ] Google sign-in redirects through Entra
- [ ] App roles appear in token claims

---

## PowerShell Commands Quick Reference

### Get Tenant ID
```powershell
Connect-AzAccount
$context = Get-AzContext
$context.Tenant.Id
```

### Create Extension Attribute
```powershell
Connect-MgGraph -Scopes "Application.ReadWrite.All"
$app = Get-MgApplication -Filter "displayName eq 'gymnastics-api'"
$params = @{
    Name = "tenant_id"
    DataType = "String"
    TargetObjects = @("User")
}
New-MgApplicationExtensionProperty -ApplicationId $app.Id -BodyParameter $params
Get-MgApplicationExtensionProperty -ApplicationId $app.Id
```

### Test Token Acquisition
```powershell
# Client credentials flow (API app)
$body = @{
    client_id = "<api-client-id>"
    scope = "https://graph.microsoft.com/.default"
    client_secret = "<client-secret>"
    grant_type = "client_credentials"
}
$response = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token" -Body $body
$response.access_token
```

---

## Notes

- Replace all `_______________` placeholders with actual values
- Keep this document secure (contains sensitive IDs)
- Update production URLs before deployment
- Store secrets in Azure Key Vault, never in source control
- Test in development environment first

---

**Template Version**: 1.0
**Created**: 2026-04-07
**Purpose**: Track Entra ID configuration values during setup
