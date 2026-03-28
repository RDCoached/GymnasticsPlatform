#!/bin/bash

# Email/Password Authentication Configuration Script for Keycloak
# This script configures the gymnastics realm to support email/password authentication
# with email verification and password reset capabilities.

set -e

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
REALM_NAME="${REALM_NAME:-gymnastics}"
ADMIN_USERNAME="${ADMIN_USERNAME:-admin}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin}"
MAILHOG_HOST="${MAILHOG_HOST:-mailhog}"
MAILHOG_PORT="${MAILHOG_PORT:-1025}"

echo "=========================================="
echo "Keycloak Email/Password Auth Configuration"
echo "=========================================="
echo "Keycloak URL: $KEYCLOAK_URL"
echo "Realm: $REALM_NAME"
echo "MailHog SMTP: $MAILHOG_HOST:$MAILHOG_PORT"
echo "=========================================="

# Wait for Keycloak to be ready
echo "Waiting for Keycloak to be ready..."
until curl -sf "$KEYCLOAK_URL/realms/master" > /dev/null 2>&1; do
  echo "  Keycloak not ready yet, waiting..."
  sleep 2
done
echo "✓ Keycloak is ready"

# Get admin access token
echo "Authenticating as admin..."
TOKEN_RESPONSE=$(curl -s -X POST "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=$ADMIN_USERNAME" \
  -d "password=$ADMIN_PASSWORD" \
  -d "grant_type=password" \
  -d "client_id=admin-cli")

ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
  echo "✗ Failed to get admin access token"
  echo "Response: $TOKEN_RESPONSE"
  exit 1
fi
echo "✓ Admin authenticated"

# Configure SMTP settings for the realm
echo "Configuring SMTP settings..."
SMTP_CONFIG=$(cat <<EOF
{
  "smtpServer": {
    "from": "noreply@gymnastics.local",
    "fromDisplayName": "Gymnastics Platform",
    "replyTo": "noreply@gymnastics.local",
    "host": "$MAILHOG_HOST",
    "port": "$MAILHOG_PORT",
    "ssl": "false",
    "starttls": "false",
    "auth": "false",
    "envelopeFrom": "noreply@gymnastics.local"
  }
}
EOF
)

SMTP_RESPONSE=$(curl -s -w "\n%{http_code}" -X PUT "$KEYCLOAK_URL/admin/realms/$REALM_NAME" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$SMTP_CONFIG")

HTTP_CODE=$(echo "$SMTP_RESPONSE" | tail -n1)
if [ "$HTTP_CODE" != "204" ] && [ "$HTTP_CODE" != "200" ]; then
  echo "✗ Failed to configure SMTP (HTTP $HTTP_CODE)"
  echo "$SMTP_RESPONSE"
  exit 1
fi
echo "✓ SMTP configured"

# Verify configuration
echo "Verifying realm configuration..."
REALM_CONFIG=$(curl -s -X GET "$KEYCLOAK_URL/admin/realms/$REALM_NAME" \
  -H "Authorization: Bearer $ACCESS_TOKEN")

REGISTRATION_ALLOWED=$(echo "$REALM_CONFIG" | grep -o '"registrationAllowed":[^,]*' | cut -d':' -f2)
VERIFY_EMAIL=$(echo "$REALM_CONFIG" | grep -o '"verifyEmail":[^,]*' | cut -d':' -f2)
RESET_PASSWORD=$(echo "$REALM_CONFIG" | grep -o '"resetPasswordAllowed":[^,]*' | cut -d':' -f2)

echo ""
echo "Configuration Summary:"
echo "  Registration Allowed: $REGISTRATION_ALLOWED"
echo "  Email Verification: $VERIFY_EMAIL"
echo "  Password Reset: $RESET_PASSWORD"
echo "  SMTP Host: $MAILHOG_HOST:$MAILHOG_PORT"
echo ""

if [ "$REGISTRATION_ALLOWED" = "true" ] && [ "$VERIFY_EMAIL" = "true" ] && [ "$RESET_PASSWORD" = "true" ]; then
  echo "✓ All settings verified successfully"
  echo ""
  echo "=========================================="
  echo "Configuration Complete!"
  echo "=========================================="
  echo "Next steps:"
  echo "  1. Access MailHog UI: http://localhost:8025"
  echo "  2. Test registration via API or frontend"
  echo "  3. Check MailHog for verification emails"
  echo "=========================================="
else
  echo "✗ Configuration incomplete. Please check realm import file."
  exit 1
fi
