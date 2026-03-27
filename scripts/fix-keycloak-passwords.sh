#!/bin/bash
set -e

echo "🔑 Fixing Keycloak user passwords..."

# Login
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh config credentials \
  --server http://localhost:8080 --realm master --user admin --password admin > /dev/null 2>&1

# Set passwords for all test users
echo "  Setting password for user@tenanta.com..."
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh set-password \
  -r gymnastics --username user@tenanta.com --new-password Test123!

echo "  Setting password for owner@tenantb.com..."
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh set-password \
  -r gymnastics --username owner@tenantb.com --new-password Test123!

echo "  Setting password for admin@platform.com..."
docker exec gymnastics-keycloak /opt/keycloak/bin/kcadm.sh set-password \
  -r gymnastics --username admin@platform.com --new-password Test123!

echo "✅ Passwords set successfully!"
echo ""
