# Secret Management Strategy

## The Problem

When working with multiple workspaces (git worktrees, multiple clones):
- Agent A adds a new secret to their local `.env` file
- Agent B (different folder) doesn't have that secret
- Secrets aren't in source control (correctly!)
- `.env` files are lost when cloning to new directories
- No way to know what secrets were added in other workspaces

## The Solution

**Hybrid Centralized Approach:**
1. **Backend (.NET):** Use `dotnet user-secrets` (already centralized per-user)
2. **Frontend (React):** Centralized `.env` directory with symlinks
3. **Setup automation:** Script creates symlinks automatically
4. **Templates:** `.env.example` files document requirements
5. **Validation:** Script checks all required secrets are present

---

## Backend: dotnet user-secrets (Already Set Up ✓)

### How It Works

**Your API already has:**
```xml
<UserSecretsId>5fa43edb-c156-488d-a76b-7021847d1c4f</UserSecretsId>
```

**Secrets are stored centrally at:**
```
~/.microsoft/usersecrets/5fa43edb-c156-488d-a76b-7021847d1c4f/secrets.json
```

**This is shared across ALL workspaces automatically!**

### Managing Backend Secrets

```bash
# Add a secret
dotnet user-secrets set "Authentication:ExternalId:ApiClientSecret" "your-secret-here" \
  --project src/GymnasticsPlatform.Api

# List all secrets
dotnet user-secrets list --project src/GymnasticsPlatform.Api

# Remove a secret
dotnet user-secrets remove "Authentication:ExternalId:ApiClientSecret" \
  --project src/GymnasticsPlatform.Api

# Clear all secrets (dangerous!)
dotnet user-secrets clear --project src/GymnasticsPlatform.Api
```

### Current Backend Secrets

Your backend already has these secrets centralized:
```
Authentication:ExternalId:TenantId
Authentication:ExternalId:CiamDomain
Authentication:ExternalId:Authority
Authentication:ExternalId:ApiClientSecret
Authentication:ExternalId:ApiClientId
```

**✅ No action needed for backend - it already works across workspaces!**

---

## Frontend: Centralized .env Files

### Directory Structure

```
~/.config/gymnastics-platform/
├── env/
│   ├── root.env                    # Root .env (docker-compose, etc.)
│   ├── user-portal.env.local       # User portal secrets
│   └── admin-portal.env.local      # Admin portal secrets
└── README.md                       # Documentation
```

### Workspace Structure (with symlinks)

```
/workspace1/feature-auth/
├── .env -> ~/.config/gymnastics-platform/env/root.env
├── frontend/
│   ├── user-portal/
│   │   └── .env.local -> ~/.config/gymnastics-platform/env/user-portal.env.local
│   └── admin-portal/
│       └── .env.local -> ~/.config/gymnastics-platform/env/admin-portal.env.local

/workspace2/feature-skills/
├── .env -> ~/.config/gymnastics-platform/env/root.env  # Same files!
├── frontend/
│   ├── user-portal/
│   │   └── .env.local -> ~/.config/gymnastics-platform/env/user-portal.env.local
│   └── admin-portal/
│       └── .env.local -> ~/.config/gymnastics-platform/env/admin-portal.env.local
```

**Changes to secrets in ANY workspace are immediately visible in ALL workspaces!**

---

## Setup: Automated Script

Run this once per workspace to set up symlinks:

```bash
./scripts/setup-secrets.sh
```

**What it does:**
1. Creates central secret directory if missing
2. Migrates existing `.env` files to central location
3. Creates symlinks from workspace to central files
4. Validates all required secrets are present
5. Shows what's missing

**Safe to run multiple times** - idempotent operation.

---

## Adding New Secrets

### Backend Secret

**In ANY workspace:**
```bash
# Add the secret (centralized automatically)
dotnet user-secrets set "NewService:ApiKey" "secret-value" \
  --project src/GymnasticsPlatform.Api

# Document in appsettings.json (use placeholder)
```

**In appsettings.json:**
```json
{
  "NewService": {
    "ApiKey": "placeholder-will-be-overridden-by-user-secrets"
  }
}
```

**All workspaces immediately have access to the secret!**

### Frontend Secret

**In ANY workspace:**
```bash
# Edit the centralized file
code ~/.config/gymnastics-platform/env/user-portal.env.local

# Add your secret
VITE_NEW_API_KEY=your-secret-here

# All workspaces see the change immediately (it's a symlink!)
```

**Update .env.example:**
```bash
# In your workspace
code frontend/user-portal/.env.example

# Add template entry
VITE_NEW_API_KEY=your-api-key-here
```

**Commit .env.example** (this documents what's needed)

---

## New Workspace Setup

### Quick Setup

```bash
# Clone repo
git clone https://github.com/RDCoached/GymnasticsPlatform new-workspace
cd new-workspace

# Run setup script
./scripts/setup-secrets.sh

# Secrets are ready! ✓
```

### What Happens

1. Script checks for centralized secrets
2. Creates symlinks to central location
3. Validates secrets match `.env.example` templates
4. Reports any missing secrets
5. You're ready to code!

**No manual secret copying needed!**

---

## Secret Lifecycle

### Adding a Secret (Full Workflow)

**Developer A (workspace1):**
```bash
# Add Entra ID client secret
dotnet user-secrets set "Authentication:ExternalId:NewSecret" "value" \
  --project src/GymnasticsPlatform.Api

# Update appsettings.json with placeholder
# Commit appsettings.json change

# Add frontend secret
echo 'VITE_OAUTH_SCOPE=api://gymnastics' >> ~/.config/gymnastics-platform/env/user-portal.env.local

# Update .env.example
echo 'VITE_OAUTH_SCOPE=your-scope-here' >> frontend/user-portal/.env.example

# Commit .env.example
git add frontend/user-portal/.env.example
git commit -m "docs: add VITE_OAUTH_SCOPE to environment template"
git push
```

**Developer B (workspace2):**
```bash
# Pull changes
git pull

# Check for new secrets
./scripts/validate-secrets.sh

# Output: "Missing: VITE_OAUTH_SCOPE"

# Add the secret (to centralized location)
echo 'VITE_OAUTH_SCOPE=api://gymnastics' >> ~/.config/gymnastics-platform/env/user-portal.env.local

# Validate again
./scripts/validate-secrets.sh

# Output: "✓ All secrets present"
```

**Backend secrets** are already there (user-secrets is centralized)!

### Rotating Secrets

```bash
# Update in central location
dotnet user-secrets set "Authentication:ExternalId:ApiClientSecret" "new-secret" \
  --project src/GymnasticsPlatform.Api

# All workspaces immediately use new secret
# No need to update each workspace!
```

### Removing Secrets

```bash
# Remove from user-secrets
dotnet user-secrets remove "OldService:ApiKey" --project src/GymnasticsPlatform.Api

# Remove from appsettings.json (commit)

# Remove from centralized .env
# Edit: ~/.config/gymnastics-platform/env/user-portal.env.local

# Remove from .env.example (commit)
```

---

## Security Best Practices

### ✅ DO

- Store real secrets in user-secrets (backend) or central .env (frontend)
- Commit `.env.example` files with placeholder values
- Add `.env*` to `.gitignore` (except `.env.example`)
- Use descriptive placeholder values: `your-tenant-id-here`
- Run validation script before starting work
- Document secrets in README or SECRET_MANAGEMENT.md

### ❌ DON'T

- Commit real secrets to git
- Share secrets via Slack/email
- Hardcode secrets in source code
- Use production secrets in development
- Store secrets in CI/CD logs

### Secret Storage Hierarchy

```
1. Development (local machine)
   Backend:  ~/.microsoft/usersecrets/<id>/secrets.json
   Frontend: ~/.config/gymnastics-platform/env/*.env.local

2. CI/CD (GitHub Actions)
   - GitHub Secrets (encrypted)
   - Environment-specific secrets

3. Production (Azure)
   - Azure Key Vault
   - Managed Identity for access
   - No secrets in code or config
```

---

## Troubleshooting

### "Secret not found in workspace"

```bash
# Check if symlink exists
ls -la .env
ls -la frontend/user-portal/.env.local

# If not, run setup
./scripts/setup-secrets.sh
```

### "Different secret values in workspaces"

**This shouldn't happen with symlinks!** But if it does:

```bash
# Check if symlink is broken
readlink .env

# Should point to: /Users/rdcoached/.config/gymnastics-platform/env/root.env

# Recreate symlink
rm .env
ln -s ~/.config/gymnastics-platform/env/root.env .env
```

### "Lost all my secrets"

**Don't panic!** Secrets are in:

```bash
# Backend secrets
cat ~/.microsoft/usersecrets/5fa43edb-c156-488d-a76b-7021847d1c4f/secrets.json

# Frontend secrets
cat ~/.config/gymnastics-platform/env/user-portal.env.local
cat ~/.config/gymnastics-platform/env/admin-portal.env.local
cat ~/.config/gymnastics-platform/env/root.env
```

**Back them up:**
```bash
# Create backup
mkdir ~/secret-backups
cp ~/.microsoft/usersecrets/5fa43edb-c156-488d-a76b-7021847d1c4f/secrets.json \
   ~/secret-backups/user-secrets-$(date +%Y%m%d).json

cp -r ~/.config/gymnastics-platform/env ~/secret-backups/env-$(date +%Y%m%d)
```

---

## Migration from Old Approach

### If You Have Existing .env Files

```bash
# Run setup script - it migrates automatically
./scripts/setup-secrets.sh

# What it does:
# 1. Copies existing .env to central location
# 2. Replaces local .env with symlink
# 3. Preserves all your secrets
# 4. Creates backup at .env.backup
```

### Manual Migration

```bash
# Create central directory
mkdir -p ~/.config/gymnastics-platform/env

# Move existing secrets
mv .env ~/.config/gymnastics-platform/env/root.env
mv frontend/user-portal/.env.local ~/.config/gymnastics-platform/env/user-portal.env.local
mv frontend/admin-portal/.env.local ~/.config/gymnastics-platform/env/admin-portal.env.local

# Create symlinks
ln -s ~/.config/gymnastics-platform/env/root.env .env
ln -s ~/.config/gymnastics-platform/env/user-portal.env.local frontend/user-portal/.env.local
ln -s ~/.config/gymnastics-platform/env/admin-portal.env.local frontend/admin-portal/.env.local

# Verify
ls -la .env  # Should show symlink
```

---

## Quick Reference

### Add Backend Secret
```bash
dotnet user-secrets set "Key:Name" "value" --project src/GymnasticsPlatform.Api
```

### Add Frontend Secret
```bash
echo 'VITE_KEY=value' >> ~/.config/gymnastics-platform/env/user-portal.env.local
```

### List Backend Secrets
```bash
dotnet user-secrets list --project src/GymnasticsPlatform.Api
```

### View Frontend Secrets
```bash
cat ~/.config/gymnastics-platform/env/user-portal.env.local
```

### Setup New Workspace
```bash
./scripts/setup-secrets.sh
```

### Validate Secrets
```bash
./scripts/validate-secrets.sh
```

### Check Secret Locations
```bash
# Backend
ls -la ~/.microsoft/usersecrets/5fa43edb-c156-488d-a76b-7021847d1c4f/

# Frontend
ls -la ~/.config/gymnastics-platform/env/
```

---

## Benefits

✅ **Single source of truth** - Secrets stored centrally
✅ **Automatic synchronization** - All workspaces share secrets
✅ **No manual copying** - Symlinks handle everything
✅ **Version controlled templates** - `.env.example` documents requirements
✅ **Backend already done** - `dotnet user-secrets` works perfectly
✅ **Easy onboarding** - New workspaces auto-configure
✅ **No lost secrets** - Persist across workspace deletions
✅ **Secure** - Never committed to git
✅ **Standard tooling** - Uses .NET built-ins

---

## Next Steps

1. **Run setup script** in current workspace
2. **Verify secrets** are working
3. **Create backups** of centralized secrets
4. **Document** any project-specific secrets
5. **Share** this guide with team members
