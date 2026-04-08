# Microsoft Entra ID Setup Guide

This guide walks through configuring Microsoft Entra ID (formerly Azure AD) for the Gymnastics Platform authentication migration.

## Prerequisites

- Azure subscription with Entra ID access
- Global Administrator or Application Administrator role
- Google OAuth credentials (for identity federation)

## Overview

We'll create 4 app registrations:
1. **API App** - Backend JWT validation + Microsoft Graph access
2. **User Portal SPA** - User authentication (React app)
3. **Admin Portal SPA** - Admin authentication (React app)
4. **Google Identity Provider** - Federated authentication

---

## Part 1: API App Registration

### Step 1: Create API App

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Go to **Microsoft Entra ID** → **App registrations**
3. Click **New registration**
4. Configure:
   - **Name**: `gymnastics-api`
   - **Supported account types**: "Accounts in any organizational directory and personal Microsoft accounts"
   - **Redirect URI**: Leave blank (this is an API)
5. Click **Register**

### Step 2: Configure API Permissions

1. In the app, go to **API permissions**
2. Click **Add a permission**
3. Select **Microsoft Graph** → **Application permissions**
4. Add: `User.ReadWrite.All`
5. Click **Grant admin consent** (requires admin)

### Step 3: Create Client Secret

1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Description: `GymnasticsAPI-Production`
4. Expires: 24 months (or custom)
5. Click **Add**
6. **COPY THE SECRET VALUE** - you won't see it again!
7. Store in Azure Key Vault or environment variables

### Step 4: Expose an API

1. Go to **Expose an API**
2. Click **Add a scope**
3. Accept the default Application ID URI or set to: `api://gymnastics-api`
4. Configure scope:
   - **Scope name**: `user.access`
   - **Who can consent**: Admins and users
   - **Admin consent display name**: "Access Gymnastics Platform API"
   - **Admin consent description**: "Allows the application to access the Gymnastics Platform API on behalf of the user"
   - **User consent display name**: "Access your gymnastics data"
   - **User consent description**: "Allows the application to access your gymnastics data"
   - **State**: Enabled
5. Click **Add scope**

### Step 5: Create Extension Attribute

1. Go to **Manifest**
2. Find the `requiredResourceAccess` array
3. Add custom attribute (done via Microsoft Graph API - see script below)

**PowerShell Script to Create Extension Attribute:**

```powershell
# Install Microsoft Graph PowerShell module if not already installed
Install-Module Microsoft.Graph -Scope CurrentUser

# Connect to Microsoft Graph
Connect-MgGraph -Scopes "Application.ReadWrite.All"

# Get the API app
$app = Get-MgApplication -Filter "displayName eq 'gymnastics-api'"

# Create extension attribute
$params = @{
    Name = "tenant_id"
    DataType = "String"
    TargetObjects = @("User")
}

New-MgApplicationExtensionProperty -ApplicationId $app.Id -BodyParameter $params

# Note the full extension attribute name (e.g., extension_abc123def456_tenant_id)
Get-MgApplicationExtensionProperty -ApplicationId $app.Id
```

### Step 6: Add App Role

1. Go to **App roles**
2. Click **Create app role**
3. Configure:
   - **Display name**: Platform Administrator
   - **Allowed member types**: Users/Groups
   - **Value**: `platform_admin`
   - **Description**: Platform-wide administrator access
4. Click **Apply**

### Step 7: Configure Optional Claims

1. Go to **Token configuration**
2. Click **Add optional claim**
3. Token type: **ID** and **Access**
4. Add claims:
   - `email`
   - `name`
   - `extension_tenant_id` (your extension attribute)
5. Check "Turn on the Microsoft Graph email, profile permission"
6. Click **Add**

### Step 8: Record Configuration

Save these values for `appsettings.json`:

```json
{
  "Authentication": {
    "EntraId": {
      "TenantId": "<your-tenant-id>",
      "ApiClientId": "<api-app-client-id>",
      "ApiClientSecret": "<from-step-3>",
      "Instance": "https://login.microsoftonline.com/",
      "Audience": "api://gymnastics-api",
      "ExtensionAppId": "<app-object-id-without-dashes>",
      "TenantIdExtensionAttributeName": "extension_<app-id>_tenant_id"
    }
  }
}
```

---

## Part 2: User Portal SPA Registration

### Step 1: Create SPA App

1. **App registrations** → **New registration**
2. Configure:
   - **Name**: `gymnastics-user-portal`
   - **Supported account types**: "Accounts in any organizational directory and personal Microsoft accounts"
   - **Redirect URI**:
     - Platform: **Single-page application**
     - URI: `http://localhost:5173/auth/callback`
3. Click **Register**

### Step 2: Configure Authentication

1. Go to **Authentication**
2. Under **Single-page application**, add redirect URIs:
   - `http://localhost:5173/auth/callback` (dev)
   - `https://app.gymnastics.example.com/auth/callback` (prod - replace domain)
3. **Front-channel logout URL**: `https://app.gymnastics.example.com/sign-in`
4. **Implicit grant and hybrid flows**: DISABLED (use PKCE instead)
5. **Allow public client flows**: No
6. Click **Save**

### Step 3: Add API Permissions

1. Go to **API permissions**
2. Click **Add a permission**
3. Select **My APIs** → **gymnastics-api**
4. Select **Delegated permissions**
5. Check `user.access`
6. Click **Add permissions**
7. Also add Microsoft Graph delegated permissions:
   - `openid`
   - `profile`
   - `email`
8. Click **Grant admin consent**

### Step 4: Configure Token

1. Go to **Token configuration**
2. Click **Add optional claim**
3. Token type: **ID**
4. Add: `email`, `name`
5. Click **Add**

### Step 5: Record Configuration

Save these values for `.env`:

```bash
VITE_ENTRA_CLIENT_ID=<user-portal-client-id>
VITE_ENTRA_TENANT_ID=<your-tenant-id>
VITE_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_API_SCOPE=api://gymnastics-api/user.access
```

---

## Part 3: Admin Portal SPA Registration

**Repeat Part 2** with these differences:
- **Name**: `gymnastics-admin-portal`
- **Redirect URIs**:
  - `http://localhost:5174/auth/callback` (dev)
  - `https://admin.gymnastics.example.com/auth/callback` (prod)

---

## Part 4: Google Identity Federation

### Step 1: Get Google OAuth Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable **Google+ API**
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Configure:
   - Application type: **Web application**
   - Name: `Gymnastics Platform Entra Federation`
   - Authorized redirect URIs:
     - `https://login.microsoftonline.com/<your-tenant-id>/oauth2/v2.0/authorize`
6. Copy **Client ID** and **Client Secret**

### Step 2: Configure in Entra ID

1. In Azure Portal → **Microsoft Entra ID**
2. Go to **External Identities** → **All identity providers**
3. Click **New Google identity provider**
4. Configure:
   - **Name**: Google
   - **Client ID**: `<from-google-step-5>`
   - **Client secret**: `<from-google-step-5>`
5. Click **Save**

### Step 3: Test Federation

1. Go to **User flows** (if using B2C) or test directly
2. When users sign in, they'll see option to sign in with Google
3. Domain hint: `google` (use in MSAL config)

---

## Part 5: Verification Checklist

### API App
- [ ] Client ID recorded
- [ ] Client secret stored securely
- [ ] Microsoft Graph permissions granted (admin consent)
- [ ] Extension attribute created (`tenant_id`)
- [ ] App role created (`platform_admin`)
- [ ] Optional claims configured
- [ ] Exposed API scope created

### User Portal SPA
- [ ] Client ID recorded
- [ ] Redirect URIs configured
- [ ] API permissions granted
- [ ] PKCE enabled (implicit flow disabled)

### Admin Portal SPA
- [ ] Client ID recorded
- [ ] Redirect URIs configured
- [ ] API permissions granted

### Google Federation
- [ ] Google OAuth app created
- [ ] Entra ID identity provider configured
- [ ] Can test Google sign-in

---

## Part 6: Update Application Configuration

### Backend (`appsettings.json`)

```json
{
  "Authentication": {
    "ActiveProvider": "Keycloak",
    "EntraId": {
      "TenantId": "<your-tenant-id>",
      "ApiClientId": "<api-app-client-id>",
      "ApiClientSecret": "stored-in-keyvault",
      "Instance": "https://login.microsoftonline.com/",
      "Audience": "api://gymnastics-api",
      "ExtensionAppId": "<app-object-id-without-dashes>",
      "TenantIdExtensionAttributeName": "extension_<appid>_tenant_id"
    },
    "Keycloak": {
      "Realm": "gymnastics",
      "AdminBaseUrl": "http://localhost:8080",
      "AdminUsername": "admin",
      "AdminPassword": "admin",
      "AdminClientId": "admin-cli"
    }
  }
}
```

### Frontend User Portal (`.env`)

```bash
# Auth Provider Selection
VITE_AUTH_PROVIDER=keycloak

# Entra ID Configuration
VITE_ENTRA_CLIENT_ID=<user-portal-client-id>
VITE_ENTRA_TENANT_ID=<your-tenant-id>
VITE_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_API_SCOPE=api://gymnastics-api/user.access

# Keycloak (existing)
VITE_KEYCLOAK_URL=http://localhost:8080
VITE_KEYCLOAK_REALM=gymnastics
VITE_KEYCLOAK_CLIENT_ID=user-portal
```

### Frontend Admin Portal (`.env`)

```bash
VITE_AUTH_PROVIDER=keycloak
VITE_ENTRA_CLIENT_ID=<admin-portal-client-id>
VITE_ENTRA_TENANT_ID=<your-tenant-id>
VITE_REDIRECT_URI=http://localhost:5174/auth/callback
VITE_API_SCOPE=api://gymnastics-api/user.access
```

---

## Part 7: Security Best Practices

### Secrets Management

**Development:**
```bash
# Use dotnet user-secrets
dotnet user-secrets set "Authentication:EntraId:ApiClientSecret" "<your-secret>"
```

**Production:**
- Store in **Azure Key Vault**
- Reference in configuration:
  ```json
  {
    "Authentication": {
      "EntraId": {
        "ApiClientSecret": "{{keyvault-reference}}"
      }
    }
  }
  ```

### HTTPS Requirements

- **Development**: Use HTTPS for redirect URIs (configure SSL cert)
- **Production**: Enforce HSTS, HTTPS-only cookies

### CORS Configuration

Update `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
            "https://app.gymnastics.example.com",
            "https://admin.gymnastics.example.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

---

## Troubleshooting

### Issue: "Invalid redirect URI"
- Ensure exact match between configured URI and callback URL
- Check for trailing slashes
- Verify HTTPS vs HTTP

### Issue: "Admin consent required"
- Go to API permissions → Grant admin consent
- Requires Global Administrator role

### Issue: Extension attribute not appearing in token
- Verify optional claims are configured
- Check manifest for extension attribute mapping
- May take 5-10 minutes to propagate

### Issue: Google federation not working
- Verify Google OAuth redirect URI exactly matches Entra format
- Check Google Cloud Console for errors
- Ensure Google+ API is enabled

---

## Next Steps

After completing this setup:

1. ✅ Verify all app registrations created
2. ✅ Test token acquisition using Postman/curl
3. ✅ Proceed to **Phase 3**: Implement EntraIdAuthenticationProvider
4. ✅ Update frontend to use MSAL

---

## Reference Links

- [Microsoft Entra ID Documentation](https://learn.microsoft.com/en-us/entra/identity/)
- [App Registration Guide](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)
- [Extension Attributes](https://learn.microsoft.com/en-us/graph/extensibility-overview)
- [Google Identity Federation](https://learn.microsoft.com/en-us/entra/external-id/google-federation)
- [MSAL.js Documentation](https://learn.microsoft.com/en-us/entra/msal/javascript/)

---

**Document Version**: 1.0
**Last Updated**: 2026-04-07
**Author**: Claude Code
**Status**: Phase 2 - Configuration Guide
