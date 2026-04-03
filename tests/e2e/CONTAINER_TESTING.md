# Container-Based E2E Testing

This guide explains how to run E2E tests in a containerized environment that exactly mirrors the CI pipeline.

## Why Container-Based Testing?

**Problem**: E2E tests that pass locally but fail in CI (or vice versa) due to environment differences.

**Solution**: Run tests in the exact same containerized environment both locally and in CI.

## Quick Start

```bash
# From project root
./scripts/run-e2e-local.sh
```

This script will:
1. Clean up any existing E2E containers
2. Build fresh backend and frontend Docker images
3. Start PostgreSQL, backend, and frontend services
4. Wait for all services to be healthy
5. Run Playwright E2E tests
6. Stop and clean up containers

## Manual Container Management

If you want more control over the container lifecycle:

### Start E2E Environment

```bash
# Start all services
docker-compose -f docker-compose.e2e.yml up -d

# Watch logs
docker-compose -f docker-compose.e2e.yml logs -f

# Check service health
docker-compose -f docker-compose.e2e.yml ps
```

### Run Tests Against Running Environment

```bash
cd tests/e2e
npm test                # Interactive mode
npm run test:ci         # CI mode (run once)
npm run test:headed     # See browser
npm run test:ui         # Playwright UI
```

### Stop E2E Environment

```bash
# Stop services
docker-compose -f docker-compose.e2e.yml down

# Stop and remove volumes (clean slate)
docker-compose -f docker-compose.e2e.yml down -v
```

## Architecture

The E2E environment consists of three services:

### 1. PostgreSQL Database (`postgres-e2e`)
- **Image**: `postgres:17`
- **Port**: `5433` (to avoid conflicts with local PostgreSQL on 5432)
- **Database**: `gymnastics_test`
- **Health Check**: `pg_isready` every 5s

### 2. Backend API (`backend-e2e`)
- **Build**: Multi-stage .NET 10 build from `Dockerfile.e2e.backend`
- **Port**: `5001`
- **Environment**:
  - `ASPNETCORE_ENVIRONMENT=Development`
  - `E2E_MODE=true` (enables TestAuthenticationHandler)
  - Connection string pointing to `postgres-e2e`
- **Health Check**: `curl http://localhost:5001/health` every 5s
- **Dependencies**: Waits for `postgres-e2e` to be healthy before starting

### 3. Frontend App (`frontend-e2e`)
- **Build**: Node 20 Alpine with Vite dev server from `frontend/user-portal/Dockerfile.e2e`
- **Port**: `3001`
- **Build Args**:
  - `VITE_API_URL=http://localhost:5001`
  - `VITE_E2E_MODE=true` (enables TestAuthProvider)
- **Health Check**: `curl http://localhost:3001` every 5s
- **Dependencies**: Waits for `backend-e2e` to be healthy before starting

All services are connected via a dedicated `e2e-network` bridge network.

## Environment Variables

The containerized setup uses the exact same environment configuration as CI:

**Backend** (`E2E_MODE=true`):
- Registers `TestAuthenticationHandler` instead of Keycloak authentication
- Uses in-memory test authentication with hardcoded JWT claims

**Frontend** (`VITE_E2E_MODE=true`):
- Uses `TestAuthProvider` instead of `KeycloakAuthProvider`
- Authenticates directly against backend `/api/auth/login` endpoint
- Skips Keycloak entirely

## Differences from CI

The local containerized environment is nearly identical to CI, with these intentional differences:

| Aspect | Local Container | CI (GitHub Actions) |
|--------|----------------|---------------------|
| Container orchestration | Docker Compose | GitHub Actions services |
| PostgreSQL port | 5433 | 5432 |
| Test execution | Host machine | Container |
| Build caching | Docker layer cache | GitHub Actions cache |
| Service startup | Sequential with health checks | Parallel with health checks |

## Debugging Failed Tests

When tests fail in the containerized environment:

### 1. Check Service Logs

```bash
# All services
docker-compose -f docker-compose.e2e.yml logs

# Specific service
docker-compose -f docker-compose.e2e.yml logs backend-e2e
docker-compose -f docker-compose.e2e.yml logs frontend-e2e
docker-compose -f docker-compose.e2e.yml logs postgres-e2e
```

### 2. Check Service Health

```bash
docker-compose -f docker-compose.e2e.yml ps
```

All services should show "healthy" status. If not, check logs.

### 3. Inspect Containers

```bash
# Execute commands in running containers
docker-compose -f docker-compose.e2e.yml exec backend-e2e curl http://localhost:5001/health
docker-compose -f docker-compose.e2e.yml exec frontend-e2e curl http://localhost:3001

# Open shell in container
docker-compose -f docker-compose.e2e.yml exec backend-e2e /bin/bash
docker-compose -f docker-compose.e2e.yml exec frontend-e2e /bin/sh
```

### 4. Run Tests in Debug Mode

```bash
cd tests/e2e

# Run with browser visible
npm run test:headed

# Run with Playwright UI (interactive debugging)
npm run test:ui

# Run specific test
npx playwright test tests/auth.spec.ts --headed
```

### 5. Check Database State

```bash
# Connect to PostgreSQL
docker-compose -f docker-compose.e2e.yml exec postgres-e2e psql -U postgres -d gymnastics_test

# List tables
\dt

# Query data
SELECT * FROM user_profiles;
SELECT * FROM clubs;
```

## Rebuilding After Code Changes

After making code changes, rebuild the affected service:

```bash
# Rebuild backend
docker-compose -f docker-compose.e2e.yml build backend-e2e
docker-compose -f docker-compose.e2e.yml up -d backend-e2e

# Rebuild frontend
docker-compose -f docker-compose.e2e.yml build frontend-e2e
docker-compose -f docker-compose.e2e.yml up -d frontend-e2e

# Rebuild everything
docker-compose -f docker-compose.e2e.yml build
docker-compose -f docker-compose.e2e.yml up -d
```

## CI/CD Integration

The GitHub Actions E2E workflow (`.github/workflows/e2e-tests.yml`) uses the same configuration but runs services as GitHub Actions services instead of Docker Compose.

Key differences:
- CI starts services in parallel (faster)
- CI uses artifacts to capture test results and videos
- CI has stricter timeouts (20 minutes total)

## Troubleshooting

### Services Won't Start

**Problem**: `docker-compose up` fails with port conflicts

**Solution**:
```bash
# Check what's using the ports
lsof -i :5001  # Backend
lsof -i :3001  # Frontend
lsof -i :5433  # PostgreSQL

# Kill processes or stop conflicting containers
docker ps
docker stop <container_id>
```

### Tests Timeout Waiting for Services

**Problem**: Health checks never pass, services stuck in "starting"

**Solution**:
```bash
# Check logs for errors
docker-compose -f docker-compose.e2e.yml logs backend-e2e
docker-compose -f docker-compose.e2e.yml logs frontend-e2e

# Common issues:
# - Backend: Migration failures, missing environment variables
# - Frontend: Build errors, incorrect VITE_ variables
# - PostgreSQL: Insufficient resources, corrupt data volume
```

### Database Migration Errors

**Problem**: Backend fails to start with migration errors

**Solution**:
```bash
# Clean database volume
docker-compose -f docker-compose.e2e.yml down -v

# Restart with fresh database
docker-compose -f docker-compose.e2e.yml up -d
```

### Frontend Build Failures

**Problem**: Frontend container exits immediately

**Solution**:
```bash
# Check build logs
docker-compose -f docker-compose.e2e.yml logs frontend-e2e

# Rebuild with no cache
docker-compose -f docker-compose.e2e.yml build --no-cache frontend-e2e
docker-compose -f docker-compose.e2e.yml up -d frontend-e2e
```

## Best Practices

1. **Clean slate before important test runs**:
   ```bash
   docker-compose -f docker-compose.e2e.yml down -v
   ./scripts/run-e2e-local.sh
   ```

2. **Keep containers running during development**:
   - Start containers once: `docker-compose -f docker-compose.e2e.yml up -d`
   - Run tests repeatedly: `cd tests/e2e && npm test`
   - Rebuild only when code changes affect services

3. **Use CI mode for pre-commit checks**:
   ```bash
   cd tests/e2e && npm run test:ci
   ```

4. **Check logs after failures**:
   ```bash
   docker-compose -f docker-compose.e2e.yml logs
   ```

5. **Parallel test execution**:
   ```bash
   cd tests/e2e
   npm run test:ci -- --workers=4
   ```

## Performance Tips

- **Layer caching**: Docker caches build layers. Rebuild is fast if dependencies haven't changed.
- **Volume mounts**: Not used in E2E setup to ensure isolation. Each run gets fresh containers.
- **Parallel workers**: Playwright can run tests in parallel. Adjust `--workers=N` based on CPU cores.
- **Headed vs headless**: Headless mode (default) is faster. Use headed only for debugging.

## Migration from Non-Containerized E2E

If you were previously running E2E tests directly on your host machine:

**Old workflow**:
```bash
# Start backend locally
dotnet run --project src/GymnasticsPlatform.Api

# Start frontend locally
cd frontend/user-portal && npm run dev

# Run tests
cd tests/e2e && npm test
```

**New workflow**:
```bash
# Everything in containers
./scripts/run-e2e-local.sh
```

**Benefits**:
- ✅ Identical to CI environment
- ✅ No "works on my machine" issues
- ✅ Isolated database (no pollution from dev work)
- ✅ Clean slate every run
- ✅ No manual environment variable setup
