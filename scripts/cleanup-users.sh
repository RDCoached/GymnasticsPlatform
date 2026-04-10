#!/usr/bin/env bash
set -euo pipefail

echo "🧹 Cleaning up all test users from External ID and Database"
echo ""

# Load environment variables
if [ -f infrastructure/external-id/external-id-config.env ]; then
    export $(grep -v '^#' infrastructure/external-id/external-id-config.env | xargs)
fi

# Check required variables
: "${CIAM_TENANT_ID:?Error: CIAM_TENANT_ID not set}"
: "${API_CLIENT_ID:?Error: API_CLIENT_ID not set}"
: "${API_CLIENT_SECRET:?Error: API_CLIENT_SECRET not set}"

echo "📋 Step 1: Get access token for Graph API..."
TOKEN_RESPONSE=$(curl -s -X POST \
    "https://login.microsoftonline.com/${CIAM_TENANT_ID}/oauth2/v2.0/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "client_id=${API_CLIENT_ID}" \
    -d "client_secret=${API_CLIENT_SECRET}" \
    -d "scope=https://graph.microsoft.com/.default" \
    -d "grant_type=client_credentials")

ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token // empty')

if [ -z "$ACCESS_TOKEN" ]; then
    echo "❌ Failed to get access token"
    echo "$TOKEN_RESPONSE" | jq .
    exit 1
fi

echo "✅ Got access token"
echo ""

echo "📋 Step 2: List all users in External ID..."
USERS_RESPONSE=$(curl -s --get \
    "https://graph.microsoft.com/v1.0/users" \
    --data-urlencode "\$select=id,userPrincipalName,displayName" \
    -H "Authorization: Bearer $ACCESS_TOKEN")

USER_COUNT=$(echo "$USERS_RESPONSE" | jq -r '.value | length')
echo "Found $USER_COUNT users"

if [ "$USER_COUNT" -eq 0 ]; then
    echo "ℹ️  No users to delete in External ID"
else
    echo ""
    echo "Users to delete:"
    echo "$USERS_RESPONSE" | jq -r '.value[] | "  - \(.displayName) (\(.userPrincipalName))"'
    echo ""

    read -p "Delete these $USER_COUNT users from External ID? (y/N) " -n 1 -r
    echo

    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "🗑️  Deleting users from External ID..."

        echo "$USERS_RESPONSE" | jq -r '.value[].id' | while read -r user_id; do
            DELETE_RESPONSE=$(curl -s -w "\n%{http_code}" -X DELETE \
                "https://graph.microsoft.com/v1.0/users/${user_id}" \
                -H "Authorization: Bearer $ACCESS_TOKEN")

            HTTP_CODE=$(echo "$DELETE_RESPONSE" | tail -n 1)

            if [ "$HTTP_CODE" -eq 204 ]; then
                echo "  ✅ Deleted user: $user_id"
            else
                echo "  ❌ Failed to delete user: $user_id (HTTP $HTTP_CODE)"
            fi
        done

        echo "✅ External ID cleanup complete"
    else
        echo "⏭️  Skipping External ID cleanup"
    fi
fi

echo ""
echo "📋 Step 3: Clear database tables..."
echo ""

# Get database connection string from docker-compose
DB_PASSWORD="gymnastics_password_dev"
DB_NAME="gymnastics_dev"

read -p "Clear all data from database tables? (y/N) " -n 1 -r
echo

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "🗑️  Clearing database..."

    docker exec gymnastics-postgres psql -U postgres -d "$DB_NAME" <<'EOSQL'
-- Clear all user data
TRUNCATE TABLE user_role_mappings CASCADE;
TRUNCATE TABLE club_invites CASCADE;
TRUNCATE TABLE clubs CASCADE;
TRUNCATE TABLE user_profiles CASCADE;

-- Verify cleanup
SELECT 'user_profiles' as table_name, COUNT(*) as count FROM user_profiles
UNION ALL
SELECT 'clubs', COUNT(*) FROM clubs
UNION ALL
SELECT 'club_invites', COUNT(*) FROM club_invites
UNION ALL
SELECT 'user_role_mappings', COUNT(*) FROM user_role_mappings;
EOSQL

    echo "✅ Database cleanup complete"
else
    echo "⏭️  Skipping database cleanup"
fi

echo ""
echo "📋 Step 4: Clear Redis sessions..."
echo ""

read -p "Clear all sessions from Redis? (y/N) " -n 1 -r
echo

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "🗑️  Flushing Redis..."
    docker exec gymnastics-redis redis-cli FLUSHALL
    echo "✅ Redis cleanup complete"
else
    echo "⏭️  Skipping Redis cleanup"
fi

echo ""
echo "✅ Cleanup complete! You can now register fresh users."
