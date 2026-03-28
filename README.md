# Gymnastics Session Planner - Phase 1: Foundation Infrastructure

A modular monolith application for gymnastics session planning with multi-tenancy, authentication, and observability.

## Architecture

**Hybrid Modular Monolith** with **Onion Architecture** per module:
- Each module (Auth, Sessions) follows Onion layers: Domain → Application → Infrastructure → API
- Common library contains contracts and shared types
- Multi-tenancy via shared schema with TenantId column
- Event-driven communication between modules (MediatR)

## Technology Stack

### Backend
- **.NET 10** with Minimal APIs
- **PostgreSQL 16** with multi-tenant support
- **Keycloak 26** for authentication (Google OAuth + JWT)
- **EF Core 10** with global query filters
- **OpenTelemetry** for observability

### Frontend
- **React 19** + **TypeScript 5.7**
- **Vite** build tooling
- **@react-keycloak/web** for authentication
- Two SPAs: User Portal (3001), Admin Portal (3002)

### Observability
- **Local Dev**: Grafana LGTM stack (Loki, Grafana, Tempo, Prometheus)
- **Production**: Azure Application Insights (same instrumentation)

## Documentation

- [Onboarding Flow Implementation](docs/ONBOARDING_FLOW.md) - Complete guide to the user onboarding system, tenant assignment, and Keycloak integration
- [Keycloak Setup](KEYCLOAK_SETUP.md) - Keycloak configuration, Google OAuth setup, and JWT authentication

## Quick Start

### Prerequisites
- .NET 10 SDK
- Docker Desktop
- Node.js 20+ (for frontend)

### 1. Start Infrastructure

```bash
# Start PostgreSQL, Keycloak, and Grafana stack
docker-compose up -d

# Wait for services to be healthy
docker-compose ps
```

### 2. Run Backend API

```bash
dotnet run --project src/GymnasticsPlatform.Api
```

API will be available at: http://localhost:5001

### 3. Run Frontend Applications

**User Portal:**
```bash
cd frontend/user-portal
npm install
npm run dev
```
User Portal will be available at: http://localhost:3001

**Admin Portal:**
```bash
cd frontend/admin-portal
npm install
npm run dev
```
Admin Portal will be available at: http://localhost:3002

### 4. Verify Setup

**Health Check:**
```bash
curl http://localhost:5001/health
```

**Protected Endpoint (requires authentication):**
```bash
curl http://localhost:5001/api/auth/me \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

## Testing & Code Coverage

### Backend Tests

**Run all backend tests:**
```bash
dotnet test
```

**Run specific test project:**
```bash
# Domain tests
dotnet test tests/Auth.Domain.Tests

# Integration tests (uses TestContainers for real PostgreSQL)
dotnet test tests/GymnasticsPlatform.Integration.Tests

# Application tests
dotnet test tests/Auth.Application.Tests
```

**Run tests with coverage report:**
```bash
./scripts/test-with-coverage.sh
```

This will:
- Run all tests with code coverage
- Generate HTML reports in `coverage-report/`
- Display coverage summary
- Open the HTML report (macOS)

### Frontend Tests

**User Portal:**
```bash
cd frontend/user-portal
npm test                # Run tests in watch mode
npm run test:ci         # Run tests once (for CI)
npm run test:coverage   # Run tests with coverage report
```

**Admin Portal:**
```bash
cd frontend/admin-portal
npm test                # Run tests in watch mode
npm run test:ci         # Run tests once (for CI)
npm run test:coverage   # Run tests with coverage report
```

### Coverage Requirements
- Backend: Minimum 80% line coverage (enforced in CI)
- CRAP analysis for identifying high-risk code
- Reports uploaded to Codecov on CI builds

## Project Structure

```
GymnasticsPlatform/
├── src/
│   ├── Common/
│   │   ├── Common.Core/              # Result pattern, ITenantContext, IMultiTenant
│   │   └── Common.Contracts/         # Integration events
│   │
│   ├── Modules/
│   │   ├── Auth/                     # Auth module (Onion Architecture)
│   │   │   ├── Auth.Domain/          # Domain entities (UserProfile)
│   │   │   ├── Auth.Application/     # Use cases, commands, queries
│   │   │   ├── Auth.Infrastructure/  # EF Core DbContext, repositories
│   │   │   └── Auth.Api/             # Endpoints, DTOs
│   │   │
│   │   └── Sessions/                 # Sessions module (Onion Architecture)
│   │       ├── Sessions.Domain/
│   │       ├── Sessions.Application/
│   │       ├── Sessions.Infrastructure/
│   │       └── Sessions.Api/
│   │
│   └── GymnasticsPlatform.Api/       # Composition root
│
├── frontend/
│   ├── user-portal/                  # React SPA (port 3001)
│   └── admin-portal/                 # React SPA (port 3002)
│
├── .github/workflows/
│   └── ci.yml                        # CI/CD pipeline
│
├── docker-compose.yml
└── README.md
```

## Multi-Tenancy

All database queries are automatically filtered by `TenantId`:

```csharp
// UserProfile entity implements IMultiTenant
public sealed class UserProfile : IMultiTenant
{
    public Guid TenantId { get; private set; }
    // ...
}

// DbContext automatically filters all queries
var users = await _db.UserProfiles.ToListAsync(); // Already filtered!
```

TenantId is extracted from JWT `tenant_id` claim and injected via `ITenantContext`.

## Development Workflow

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Create Migration
```bash
dotnet ef migrations add <MigrationName> \
  --project src/Modules/Auth/Auth.Infrastructure \
  --startup-project src/GymnasticsPlatform.Api \
  --context AuthDbContext
```

### Apply Migrations
Migrations are automatically applied on startup in Development mode.

## CI/CD Pipeline

GitHub Actions workflow runs on every push to `main` or PR:

1. ✅ Build solution
2. ✅ Run tests with coverage
3. ✅ Generate coverage reports
4. ✅ Upload to Codecov
5. ✅ Enforce 80% coverage threshold

## Access Points

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5001 | N/A |
| Keycloak Admin | http://localhost:8080 | admin / admin |
| Grafana | http://localhost:3000 | admin / admin |
| Adminer (DB UI) | http://localhost:8081 | gymadmin / local_dev_123 |
| User Portal | http://localhost:3001 | (Keycloak SSO) |
| Admin Portal | http://localhost:3002 | (Keycloak SSO) |

## Phase 1 Status

✅ Solution structure with 2 modules (Auth, Sessions)
✅ PostgreSQL with multi-tenancy
✅ Keycloak authentication
✅ OpenTelemetry observability
✅ Docker Compose for local development
✅ Code coverage infrastructure
✅ CI/CD pipeline with GitHub Actions
⬜ React SPAs scaffolding (pending)
⬜ End-to-end verification script (pending)

## Next Steps

- Phase 2: Business domain (Coaches, Athletes, Sessions planning)
- Phase 3: RAG integration (pgvector + Semantic Kernel)
- Phase 4: Production deployment (Kubernetes + Terraform)

## License

MIT
