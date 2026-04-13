#!/usr/bin/env bash
set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}=== Secret Validation ===${NC}"
echo ""

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ERRORS=0
WARNINGS=0

# Function to check if file exists
check_file_exists() {
    local file="$1"
    local description="$2"

    if [ ! -e "$file" ]; then
        echo -e "${RED}✗${NC} Missing: $description"
        echo -e "   Expected: $file"
        ((ERRORS++))
        return 1
    fi

    if [ -L "$file" ]; then
        local target=$(readlink "$file")
        if [ ! -e "$target" ]; then
            echo -e "${RED}✗${NC} Broken symlink: $description"
            echo -e "   Points to: $target (missing)"
            ((ERRORS++))
            return 1
        fi
        echo -e "${GREEN}✓${NC} $description (symlinked to: ${target})"
    else
        echo -e "${YELLOW}⚠${NC}  $description (not symlinked - consider running setup-secrets.sh)"
        ((WARNINGS++))
    fi
    return 0
}

# Function to validate env file against example
validate_env_file() {
    local env_file="$1"
    local example_file="$2"
    local description="$3"

    if [ ! -f "$example_file" ]; then
        return 0  # No example file, skip validation
    fi

    if [ ! -f "$env_file" ]; then
        echo -e "${RED}✗${NC} $description is missing"
        ((ERRORS++))
        return 1
    fi

    echo -e "${BLUE}Validating:${NC} $description"

    # Read target file (follow symlink if needed)
    local actual_file="$env_file"
    if [ -L "$env_file" ]; then
        actual_file=$(readlink "$env_file")
    fi

    local missing_keys=()

    # Extract keys from .env.example (lines starting with KEY=)
    while IFS='=' read -r key value; do
        # Skip comments and empty lines
        [[ $key =~ ^#.*$ ]] && continue
        [[ -z $key ]] && continue

        # Check if key exists in actual env file
        if ! grep -q "^${key}=" "$actual_file" 2>/dev/null; then
            missing_keys+=("$key")
        fi
    done < <(grep -v '^#' "$example_file" | grep '=' || true)

    if [ ${#missing_keys[@]} -gt 0 ]; then
        echo -e "${RED}✗${NC} Missing keys in $description:"
        for key in "${missing_keys[@]}"; do
            echo -e "   - ${key}"
        done
        ((ERRORS++))
        return 1
    else
        echo -e "${GREEN}✓${NC} All required keys present"
        return 0
    fi
}

# Check symlinks
echo -e "${BLUE}=== Checking Environment Files ===${NC}"
echo ""

check_file_exists "${PROJECT_ROOT}/.env" "Root .env file"
check_file_exists "${PROJECT_ROOT}/frontend/user-portal/.env.local" "User portal .env.local"
check_file_exists "${PROJECT_ROOT}/frontend/admin-portal/.env.local" "Admin portal .env.local"

echo ""

# Validate against examples
echo -e "${BLUE}=== Validating Against Templates ===${NC}"
echo ""

if [ -f "${PROJECT_ROOT}/.env.example" ]; then
    validate_env_file \
        "${PROJECT_ROOT}/.env" \
        "${PROJECT_ROOT}/.env.example" \
        "Root .env"
    echo ""
fi

if [ -f "${PROJECT_ROOT}/frontend/user-portal/.env.example" ]; then
    validate_env_file \
        "${PROJECT_ROOT}/frontend/user-portal/.env.local" \
        "${PROJECT_ROOT}/frontend/user-portal/.env.example" \
        "User portal .env.local"
    echo ""
fi

if [ -f "${PROJECT_ROOT}/frontend/admin-portal/.env.example" ]; then
    validate_env_file \
        "${PROJECT_ROOT}/frontend/admin-portal/.env.local" \
        "${PROJECT_ROOT}/frontend/admin-portal/.env.example" \
        "Admin portal .env.local"
    echo ""
fi

# Check backend user-secrets
echo -e "${BLUE}=== Backend Secrets (dotnet user-secrets) ===${NC}"
echo ""

if [ -d "${PROJECT_ROOT}/src/GymnasticsPlatform.Api" ]; then
    # Required backend secrets (from appsettings.json structure)
    REQUIRED_SECRETS=(
        "Authentication:ExternalId:TenantId"
        "Authentication:ExternalId:CiamDomain"
        "Authentication:ExternalId:Authority"
        "Authentication:ExternalId:ApiClientId"
        "Authentication:ExternalId:ApiClientSecret"
    )

    # Get current secrets
    if ! secrets_output=$(dotnet user-secrets list --project "${PROJECT_ROOT}/src/GymnasticsPlatform.Api" 2>&1); then
        echo -e "${RED}✗${NC} Failed to read user-secrets"
        echo -e "   ${secrets_output}"
        ((ERRORS++))
    else
        echo -e "${BLUE}Checking required backend secrets...${NC}"

        for secret_key in "${REQUIRED_SECRETS[@]}"; do
            if echo "$secrets_output" | grep -q "^${secret_key} ="; then
                echo -e "${GREEN}✓${NC} $secret_key"
            else
                echo -e "${RED}✗${NC} Missing: $secret_key"
                ((ERRORS++))
            fi
        done

        # Count total secrets
        total_secrets=$(echo "$secrets_output" | wc -l)
        echo ""
        echo -e "${BLUE}Total backend secrets:${NC} $total_secrets"

        # Show location (macOS-compatible grep)
        user_secrets_id=$(grep -o '<UserSecretsId>.*</UserSecretsId>' "${PROJECT_ROOT}/src/GymnasticsPlatform.Api/GymnasticsPlatform.Api.csproj" 2>/dev/null | sed 's/<[^>]*>//g' || echo "")
        if [ -n "$user_secrets_id" ]; then
            secrets_path="${HOME}/.microsoft/usersecrets/${user_secrets_id}/secrets.json"
            echo -e "${BLUE}Location:${NC} ${secrets_path}"
        fi
    fi
else
    echo -e "${YELLOW}⚠${NC}  API project not found, skipping backend secret validation"
    ((WARNINGS++))
fi

echo ""

# Summary
echo -e "${BLUE}=== Summary ===${NC}"
echo ""

if [ $ERRORS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
    echo -e "${GREEN}✓ All secrets are properly configured!${NC}"
    exit 0
elif [ $ERRORS -eq 0 ]; then
    echo -e "${YELLOW}⚠ ${WARNINGS} warning(s) found${NC}"
    echo -e "   Consider running: ${BLUE}./scripts/setup-secrets.sh${NC}"
    exit 0
else
    echo -e "${RED}✗ ${ERRORS} error(s) found${NC}"
    if [ $WARNINGS -gt 0 ]; then
        echo -e "${YELLOW}⚠ ${WARNINGS} warning(s) found${NC}"
    fi
    echo ""
    echo -e "${BLUE}To fix:${NC}"
    echo -e "  1. Run setup: ${YELLOW}./scripts/setup-secrets.sh${NC}"
    echo -e "  2. Add missing secrets manually"
    echo -e "  3. Re-run validation: ${YELLOW}./scripts/validate-secrets.sh${NC}"
    echo ""
    echo -e "${BLUE}For backend secrets:${NC}"
    echo -e "  dotnet user-secrets set \"Key\" \"Value\" --project src/GymnasticsPlatform.Api"
    echo ""
    echo -e "${BLUE}For frontend secrets:${NC}"
    echo -e "  Edit: ~/.config/gymnastics-platform/env/user-portal.env.local"
    echo -e "  Edit: ~/.config/gymnastics-platform/env/admin-portal.env.local"
    exit 1
fi
