#!/usr/bin/env bash
set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== Gymnastics Platform Secret Setup ===${NC}"
echo ""

# Central secret directory
CENTRAL_SECRETS_DIR="${HOME}/.config/gymnastics-platform/env"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Create central directory if it doesn't exist
mkdir -p "$CENTRAL_SECRETS_DIR"

echo -e "${BLUE}📁 Central secrets directory: ${GREEN}${CENTRAL_SECRETS_DIR}${NC}"
echo ""

# Function to setup symlink
setup_symlink() {
    local source_file="$1"
    local target_file="$2"
    local description="$3"

    echo -e "${BLUE}Setting up:${NC} $description"

    # If target exists and is not a symlink
    if [ -e "$target_file" ] && [ ! -L "$target_file" ]; then
        echo -e "  ${YELLOW}→${NC} Found existing file, migrating to central location"

        # Backup
        cp "$target_file" "${target_file}.backup"
        echo -e "  ${GREEN}✓${NC} Created backup: ${target_file}.backup"

        # Move to central location
        mv "$target_file" "$source_file"
        echo -e "  ${GREEN}✓${NC} Moved to: ${source_file}"

    # If source doesn't exist, create from .env.example or empty
    elif [ ! -e "$source_file" ]; then
        local example_file="${target_file}.example"

        if [ -e "$example_file" ]; then
            echo -e "  ${YELLOW}→${NC} Creating from template: ${example_file}"
            cp "$example_file" "$source_file"
            echo -e "  ${GREEN}✓${NC} Created template at: ${source_file}"
            echo -e "  ${RED}⚠${NC}  ${YELLOW}You need to fill in real values!${NC}"
        else
            echo -e "  ${YELLOW}→${NC} Creating empty file"
            touch "$source_file"
            echo -e "  ${GREEN}✓${NC} Created empty: ${source_file}"
        fi
    else
        echo -e "  ${GREEN}✓${NC} Already exists: ${source_file}"
    fi

    # Remove existing target if it's a broken symlink
    if [ -L "$target_file" ] && [ ! -e "$target_file" ]; then
        echo -e "  ${YELLOW}→${NC} Removing broken symlink"
        rm "$target_file"
    fi

    # Create symlink if target doesn't exist
    if [ ! -e "$target_file" ]; then
        echo -e "  ${YELLOW}→${NC} Creating symlink"
        ln -s "$source_file" "$target_file"
        echo -e "  ${GREEN}✓${NC} Symlinked: ${target_file} → ${source_file}"
    elif [ -L "$target_file" ]; then
        local current_target=$(readlink "$target_file")
        if [ "$current_target" == "$source_file" ]; then
            echo -e "  ${GREEN}✓${NC} Symlink already correct"
        else
            echo -e "  ${YELLOW}→${NC} Updating symlink (was pointing to: ${current_target})"
            rm "$target_file"
            ln -s "$source_file" "$target_file"
            echo -e "  ${GREEN}✓${NC} Updated symlink"
        fi
    fi

    echo ""
}

# Setup root .env (for docker-compose, etc.)
setup_symlink \
    "${CENTRAL_SECRETS_DIR}/root.env" \
    "${PROJECT_ROOT}/.env" \
    "Root environment file (.env)"

# Setup user portal .env.local
setup_symlink \
    "${CENTRAL_SECRETS_DIR}/user-portal.env.local" \
    "${PROJECT_ROOT}/frontend/user-portal/.env.local" \
    "User portal environment (.env.local)"

# Setup admin portal .env.local
setup_symlink \
    "${CENTRAL_SECRETS_DIR}/admin-portal.env.local" \
    "${PROJECT_ROOT}/frontend/admin-portal/.env.local" \
    "Admin portal environment (.env.local)"

# Verify backend user-secrets
echo -e "${BLUE}=== Backend Secrets (dotnet user-secrets) ===${NC}"
echo ""

if [ -d "${PROJECT_ROOT}/src/GymnasticsPlatform.Api" ]; then
    echo -e "${BLUE}Checking user-secrets...${NC}"

    # Check if secrets exist
    if dotnet user-secrets list --project "${PROJECT_ROOT}/src/GymnasticsPlatform.Api" &>/dev/null; then
        secret_count=$(dotnet user-secrets list --project "${PROJECT_ROOT}/src/GymnasticsPlatform.Api" | wc -l)
        echo -e "${GREEN}✓${NC} Found ${secret_count} backend secret(s)"

        # Show location (macOS-compatible grep)
        user_secrets_id=$(grep -o '<UserSecretsId>.*</UserSecretsId>' "${PROJECT_ROOT}/src/GymnasticsPlatform.Api/GymnasticsPlatform.Api.csproj" 2>/dev/null | sed 's/<[^>]*>//g' || echo "")

        if [ -n "$user_secrets_id" ]; then
            secrets_path="${HOME}/.microsoft/usersecrets/${user_secrets_id}/secrets.json"
            if [ -f "$secrets_path" ]; then
                echo -e "${BLUE}📍 Location:${NC} ${secrets_path}"
            fi
        fi
    else
        echo -e "${YELLOW}⚠${NC}  No backend secrets found"
        echo -e "    Add secrets with: dotnet user-secrets set \"Key\" \"Value\" --project src/GymnasticsPlatform.Api"
    fi
else
    echo -e "${YELLOW}⚠${NC}  API project not found at: src/GymnasticsPlatform.Api"
fi

echo ""

# Create README in central location
cat > "${CENTRAL_SECRETS_DIR}/README.md" << 'EOF'
# Centralized Secrets

This directory contains environment files shared across all workspaces.

## Files

- `root.env` - Root .env file (docker-compose, infrastructure)
- `user-portal.env.local` - User portal frontend secrets
- `admin-portal.env.local` - Admin portal frontend secrets

## How It Works

Each workspace has **symlinks** pointing to these files:

```
workspace1/.env -> ~/.config/gymnastics-platform/env/root.env
workspace2/.env -> ~/.config/gymnastics-platform/env/root.env
```

**Changes to any workspace immediately affect all workspaces!**

## Backend Secrets

Backend uses `dotnet user-secrets` which is automatically centralized:

```bash
dotnet user-secrets list --project src/GymnasticsPlatform.Api
```

Stored at: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

## Safety

- **NEVER** commit these files to git
- Back up regularly
- Use `.env.example` files in git to document what's needed

## Commands

```bash
# Add backend secret
dotnet user-secrets set "Key" "Value" --project src/GymnasticsPlatform.Api

# Add frontend secret
echo 'VITE_KEY=value' >> ~/.config/gymnastics-platform/env/user-portal.env.local

# Validate secrets
cd /path/to/workspace && ./scripts/validate-secrets.sh

# Setup new workspace
cd /path/to/workspace && ./scripts/setup-secrets.sh
```

## Backup

```bash
# Create backup
cp -r ~/.config/gymnastics-platform/env ~/secret-backups/env-$(date +%Y%m%d)
cp ~/.microsoft/usersecrets/<id>/secrets.json ~/secret-backups/user-secrets-$(date +%Y%m%d).json
```
EOF

echo -e "${GREEN}✓${NC} Created README at: ${CENTRAL_SECRETS_DIR}/README.md"
echo ""

# Summary
echo -e "${BLUE}=== Summary ===${NC}"
echo ""
echo -e "${GREEN}✓${NC} Centralized secrets directory: ${CENTRAL_SECRETS_DIR}"
echo -e "${GREEN}✓${NC} Symlinks created for this workspace"
echo -e "${GREEN}✓${NC} Backend using dotnet user-secrets (already centralized)"
echo ""
echo -e "${BLUE}📚 Next steps:${NC}"
echo -e "  1. Edit central files to add your secrets:"
echo -e "     ${CENTRAL_SECRETS_DIR}/user-portal.env.local"
echo -e "     ${CENTRAL_SECRETS_DIR}/admin-portal.env.local"
echo -e "  2. Validate secrets: ${YELLOW}./scripts/validate-secrets.sh${NC}"
echo -e "  3. See docs: ${YELLOW}docs/SECRET_MANAGEMENT.md${NC}"
echo ""
echo -e "${GREEN}✨ All workspaces now share the same secrets!${NC}"
