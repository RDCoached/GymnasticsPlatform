#!/bin/bash
set -e

echo "Initializing CouchDB..."

COUCHDB_URL="http://localhost:5984"
COUCHDB_USER="${COUCHDB_USER:-admin}"
COUCHDB_PASSWORD="${COUCHDB_PASSWORD:-changeme}"
DATABASE_NAME="programmes"

# Wait for CouchDB to be ready
echo "Waiting for CouchDB to be ready..."
until curl -f -s "${COUCHDB_URL}/_up" > /dev/null 2>&1; do
    echo "  CouchDB is unavailable - sleeping"
    sleep 2
done
echo "CouchDB is ready!"

# Check if database exists
if curl -f -s -u "${COUCHDB_USER}:${COUCHDB_PASSWORD}" "${COUCHDB_URL}/${DATABASE_NAME}" > /dev/null 2>&1; then
    echo "Database '${DATABASE_NAME}' already exists."
else
    echo "Creating database '${DATABASE_NAME}'..."
    curl -X PUT -u "${COUCHDB_USER}:${COUCHDB_PASSWORD}" "${COUCHDB_URL}/${DATABASE_NAME}"
    echo ""
    echo "Database '${DATABASE_NAME}' created successfully!"
fi

# Create design document for queries (optional for Mango queries)
echo "Creating design document for queries..."
curl -X PUT -u "${COUCHDB_USER}:${COUCHDB_PASSWORD}" \
  "${COUCHDB_URL}/${DATABASE_NAME}/_design/queries" \
  -H "Content-Type: application/json" \
  -d '{
    "views": {
      "by_gymnast": {
        "map": "function(doc) { if(doc.type == \"programme\") emit(doc.gymnastId, doc); }"
      },
      "by_tenant": {
        "map": "function(doc) { if(doc.type == \"programme\") emit(doc.tenantId, doc); }"
      },
      "by_coach": {
        "map": "function(doc) { if(doc.type == \"programme\") emit(doc.coachId, doc); }"
      }
    }
  }' 2>/dev/null || echo "Design document may already exist."

echo ""
echo "CouchDB initialization complete!"
