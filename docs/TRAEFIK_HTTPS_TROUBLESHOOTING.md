# Traefik HTTPS Setup and Troubleshooting

This document covers common issues encountered when setting up HTTPS with Traefik for local development and their solutions.

## Overview

The Gymnastics Platform uses Traefik as a reverse proxy to provide clean subdomain-based URLs with HTTPS support for local development. This mimics a production-like environment and satisfies OAuth redirect URI requirements.

## Architecture

- **Traefik**: Handles HTTPS termination with self-signed certificates
- **Domains**: `*.gymnastics.localhost` (all subdomains resolve to 127.0.0.1)
- **Certificates**: Self-signed wildcard certificate for `*.gymnastics.localhost`
- **Ports**:
  - 80 (HTTP) - Redirects to HTTPS
  - 443 (HTTPS) - Main entry point
  - 8080 (Traefik Dashboard)

## Common Issues and Solutions

### 1. CORS Errors with HTTPS Origins

#### Symptom
```
Access to fetch at 'https://api.gymnastics.localhost/api/auth/me' from origin
'https://app.gymnastics.localhost' has been blocked by CORS policy:
No 'Access-Control-Allow-Origin' header is present on the requested resource.
```

#### Root Cause
The API's CORS configuration only included HTTP origins, not HTTPS origins.

#### Solution
Update `appsettings.Development.json` to include HTTPS origins:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3001",
      "http://localhost:3002",
      "https://localhost:3001",
      "https://localhost:3002",
      "http://app.gymnastics.localhost",
      "http://admin.gymnastics.localhost",
      "https://app.gymnastics.localhost",
      "https://admin.gymnastics.localhost"
    ]
  }
}
```

**Important**: After updating `appsettings.Development.json`, you must rebuild the Docker image:

```bash
# Completely rebuild without cache
docker compose down api
docker rmi gymnasticsplatform-api:latest
docker compose build --no-cache api
docker compose up -d api
```

**Why `--no-cache` is necessary**: Docker's layer caching can prevent updated configuration files from being included in the image. A full rebuild ensures the latest `appsettings.Development.json` is copied into the container.

### 2. Docker Build Not Picking Up Configuration Changes

#### Symptom
After modifying `appsettings.Development.json` and running `docker compose build api`, the container still has the old configuration.

#### Root Cause
Docker uses layer caching. If the COPY command's context hasn't changed (same files, same timestamps in parent directories), Docker reuses the cached layer instead of copying files again.

#### Solution
1. Stop and remove the container:
   ```bash
   docker compose down api
   ```

2. Remove the old image:
   ```bash
   docker rmi gymnasticsplatform-api:latest
   ```

3. Build with `--no-cache`:
   ```bash
   docker compose build --no-cache api
   ```

4. Start the container:
   ```bash
   docker compose up -d api
   ```

#### Verification
Check the file inside the container:
```bash
docker compose exec api cat /app/appsettings.Development.json
```

### 3. Self-Signed Certificate Warnings

#### Symptom
Browser shows `ERR_CERT_AUTHORITY_INVALID` or "Your connection is not private" when accessing HTTPS URLs.

#### Root Cause
The certificate is self-signed and not issued by a trusted Certificate Authority.

#### Solution
**For local development**, manually accept the certificate in your browser:

1. Visit `https://app.gymnastics.localhost` in your browser
2. Click "Advanced" or "Show Details"
3. Click "Proceed to app.gymnastics.localhost (unsafe)" or similar
4. Repeat for `https://api.gymnastics.localhost`

**Alternative**: Install the certificate in your system's trust store (macOS):
```bash
sudo security add-trusted-cert -d -r trustRoot \
  -k /Library/Keychains/System.keychain \
  docker/traefik/certs/gymnastics.localhost.crt
```

### 4. MSAL "Interaction in Progress" Error

#### Symptom
```
BrowserAuthError: interaction_in_progress: Interaction is currently in progress.
Please ensure that this interaction has been completed before calling an interactive API.
```

#### Root Cause
MSAL (Microsoft Authentication Library) thinks there's already an OAuth interaction happening. This occurs when:
- A previous OAuth attempt didn't complete cleanly
- The browser has cached interaction state
- Multiple login buttons were clicked rapidly

#### Solution
Clear the browser's MSAL state:

1. Open Browser DevTools (F12)
2. Go to "Application" tab (Chrome) or "Storage" tab (Firefox)
3. Under "Local Storage" and "Session Storage", find entries for `https://app.gymnastics.localhost`
4. Delete all entries related to MSAL (usually prefixed with `msal`)
5. Hard refresh the page (Ctrl+Shift+R or Cmd+Shift+R)

**Or**: Close all tabs with the app and open a fresh tab.

### 5. Azure Redirect URI Validation Errors

#### Symptom
OAuth flow fails with "redirect_uri mismatch" or similar error from Azure.

#### Root Cause
Azure Entra External ID only accepts HTTPS redirect URIs (or `http://localhost`). Using `http://app.gymnastics.localhost` for OAuth will fail validation.

#### Solution
1. Set up HTTPS in Traefik (see main setup)
2. Add the HTTPS redirect URI in Azure:
   - Go to Azure Portal → App Registrations → User Portal app
   - Under "Authentication" → "Web" → "Redirect URIs"
   - Add: `https://app.gymnastics.localhost/auth/callback`
   - Save

### 6. Graph API "Tenant Not Found" Errors

#### Symptom
API logs show errors when calling Microsoft Graph API:
```
Request_ResourceNotFound: The tenant for tenant guid 'xxx' does not exist
```

#### Root Cause
The `ClientSecretCredential` was using the default Azure authority (`https://login.microsoftonline.com`) instead of the CIAM authority.

#### Solution
Specify `AuthorityHost` when creating the credential:

```csharp
var options = new ClientSecretCredentialOptions
{
    AuthorityHost = new Uri(_authority) // e.g., https://gymnasticsciam.ciamlogin.com
};

var credential = new ClientSecretCredential(
    _tenantId,
    _apiClientId,
    _apiClientSecret,
    options);
```

This fix was applied in `ExternalIdAuthenticationProvider.cs`.

## Setup Checklist

When setting up HTTPS for a new developer or environment:

- [ ] Generate self-signed certificate (or use existing in `docker/traefik/certs/`)
- [ ] Update `appsettings.Development.json` with HTTPS CORS origins
- [ ] Rebuild Docker image with `--no-cache`
- [ ] Accept certificate in browser for both app and API domains
- [ ] Add HTTPS redirect URI in Azure app registration
- [ ] Test OAuth flow end-to-end
- [ ] Clear browser MSAL state if encountering "interaction in progress" errors

## Generating Self-Signed Certificates

To regenerate the wildcard certificate for `*.gymnastics.localhost`:

```bash
cd docker/traefik/certs

openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout gymnastics.localhost.key \
  -out gymnastics.localhost.crt \
  -subj "/CN=*.gymnastics.localhost" \
  -addext "subjectAltName=DNS:*.gymnastics.localhost,DNS:gymnastics.localhost"
```

**Note**: The `.key` file is excluded from git by `.gitignore` for security. The `.crt` file is committed to the repository for convenience.

## Verification Commands

### Test CORS Preflight
```bash
curl -i -X OPTIONS https://api.gymnastics.localhost/api/auth/me \
  -H "Origin: https://app.gymnastics.localhost" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: Content-Type,Authorization" \
  --insecure
```

Expected headers in response:
```
access-control-allow-origin: https://app.gymnastics.localhost
access-control-allow-credentials: true
access-control-allow-methods: GET,POST,PUT,DELETE,PATCH
access-control-allow-headers: Content-Type,Authorization,X-Tenant-Id
```

### Check API Configuration in Container
```bash
docker compose exec api cat /app/appsettings.Development.json
```

### View API Logs
```bash
docker compose logs api -f
```

## Related Documentation

- **Traefik Setup**: See main `README.md` for initial Traefik configuration
- **OAuth Setup**: See `docs/ENTRA_ID_SETUP.md` for Azure configuration
- **Infrastructure**: See `infrastructure/external-id/README.md` for Terraform setup

## Lessons Learned

1. **Always rebuild with `--no-cache`** when configuration files change, especially for files not mounted as volumes
2. **HTTPS is required for OAuth** with Azure - `http://localhost` works but `http://app.gymnastics.localhost` does not
3. **CORS must include HTTPS origins** when using HTTPS - it's not automatically inferred from HTTP origins
4. **MSAL state can persist** across page refreshes - clear browser storage when troubleshooting OAuth issues
5. **Authority matters for CIAM** - Graph API calls need `AuthorityHost` set to the CIAM authority, not the default Azure AD authority
