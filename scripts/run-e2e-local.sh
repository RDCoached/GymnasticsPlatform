#!/bin/bash
set -e

echo "🧹 Cleaning up any existing E2E containers..."
docker-compose -f docker-compose.e2e.yml down -v

echo "🏗️  Building E2E environment..."
docker-compose -f docker-compose.e2e.yml build

echo "🚀 Starting E2E services..."
docker-compose -f docker-compose.e2e.yml up -d

echo "⏳ Waiting for services to be healthy..."

# Wait for backend
timeout 90 bash -c 'until docker-compose -f docker-compose.e2e.yml exec -T backend-e2e curl -sf http://localhost:5001/health; do echo "  Backend not ready, retrying..."; sleep 2; done'
echo "✓ Backend is ready!"

# Wait for frontend
timeout 90 bash -c 'until docker-compose -f docker-compose.e2e.yml exec -T frontend-e2e curl -sf http://localhost:3001; do echo "  Frontend not ready, retrying..."; sleep 2; done'
echo "✓ Frontend is ready!"

echo "🧪 Running E2E tests..."
cd tests/e2e
npm run test:ci

echo "🛑 Stopping E2E services..."
cd ../..
docker-compose -f docker-compose.e2e.yml down

echo "✅ E2E tests complete!"
