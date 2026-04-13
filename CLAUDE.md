# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Gymnastics Session Planner** - A multi-tenant SaaS platform for gymnastics session planning with Microsoft Entra External ID authentication, React SPAs, and OpenTelemetry observability.

**Current State**: Production-ready main branch with Microsoft Entra External ID (CIAM) for OAuth 2.0 authentication with JWT tokens, Traefik reverse proxy for clean domain routing, Redis-backed sessions, and automated Azure infrastructure provisioning.

**Architecture**: Hybrid Modular Monolith with Onion Architecture per module
- Each module (Auth, Sessions) follows Domain → Application → Infrastructure → API layers
- Common libraries provide shared contracts (Common.Contracts) and core types (Common.Core)
- Multi-tenancy via shared schema with `TenantId` column and global query filters
- Event-driven communication between modules using MediatR

**Stack**:
- Backend: .NET 10, ASP.NET Core Minimal APIs, EF Core 10, PostgreSQL 16
- Frontend: React 19, TypeScript 5.7, Vite
- Auth: Microsoft Entra External ID (OAuth 2.0 + session cookies + Redis)
- Observability: OpenTelemetry (LGTM stack locally, Azure App Insights in production)
- Testing: xUnit v3, TestContainers, WebApplicationFactory, Playwright (E2E)
- Infrastructure: Terraform for Azure resources, automated provisioning scripts

## Common Commands

### Build & Test

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Auth.Domain.Tests
dotnet test tests/GymnasticsPlatform.Integration.Tests

# Run tests with coverage (generates HTML report in coverage-report/)
./scripts/test-with-coverage.sh

# Run single test
dotnet test --filter "FullyQualifiedName~NameOfTestMethod"
```

### Database Migrations

```bash
# Create migration (Auth module example)
dotnet ef migrations add <MigrationName> \
  --project src/Modules/Auth/Auth.Infrastructure \
  --startup-project src/GymnasticsPlatform.Api \
  --context AuthDbContext

# Migrations apply automatically on startup in Development mode
# For production, migrations must be applied explicitly before deployment
```

### Run Application

```bash
# Start infrastructure (PostgreSQL, Redis, Grafana stack)
docker-compose up -d

# Run backend API (http://localhost:5001)
dotnet run --project src/GymnasticsPlatform.Api

# Run user portal (http://localhost:3001)
cd frontend/user-portal && npm install && npm run dev

# Run admin portal (http://localhost:3002)
cd frontend/admin-portal && npm install && npm run dev
```

### Infrastructure Provisioning

```bash
# Provision Microsoft Entra External ID (CIAM) tenant and app registrations
cd infrastructure/external-id
./setup-external-id.sh

# Configure app registrations with redirect URIs and secrets
./configure-apps.sh

# Grant Microsoft Graph API permissions (requires admin consent)
./grant-graph-permissions.sh

# See infrastructure/external-id/README.md for full setup guide
```

### Frontend Testing

```bash
# User portal tests
cd frontend/user-portal
npm test                # Watch mode
npm run test:ci         # CI mode (run once)
npm run test:coverage   # With coverage
npm run test:ui         # Vitest UI

# Admin portal tests
cd frontend/admin-portal
npm test
npm run test:ci
npm run test:coverage
npm run test:ui
```

### E2E Tests

```bash
cd tests/e2e
npm install
npx playwright install

# Run all E2E tests
npm test

# Run in headed mode (see browser)
npm run test:headed

# Run in UI mode (interactive)
npm run test:ui

# Run specific test file
npx playwright test tests/auth.spec.ts
```

### CI/CD Workflows

Three GitHub Actions workflows run on push to `main` or PRs:

1. **CI Build and Test** (`ci.yml`) - Backend build + tests with coverage, frontend build + tests
2. **E2E Tests** (`e2e-tests.yml`) - Full-stack Playwright tests
3. **Security Scan** (`security-scan.yml`) - Security audits

### Database & Sessions

```bash
# Connect to PostgreSQL
docker exec -it gymnastics-db psql -U gymadmin -d gymnastics_dev

# Connect to Redis (session store)
docker exec -it gymnastics-redis redis-cli

# View sessions
docker exec -it gymnastics-redis redis-cli KEYS "session:*"
```

## Architecture Patterns

### Multi-Tenancy System

All authenticated users have a `TenantId` resolved by `TenantResolutionMiddleware`:
- New users start in the **Onboarding Tenant** (`00000000-0000-0000-0000-000000000001`)
- After onboarding, users are assigned to a Club Tenant or Individual Tenant
- `TenantId` is stored in Microsoft Entra ID user extension attributes and injected into `ITenantContext`
- All `IMultiTenant` entities are automatically filtered by `TenantId` via EF Core global query filters

**Key Types**:
- `IMultiTenant` interface (Common.Core) - marks entities as tenant-scoped
- `ITenantContext` interface (Common.Core) - provides current tenant ID
- `TenantContext` service (GymnasticsPlatform.Api) - reads from `HttpContext.Items["TenantId"]`
- `TenantResolutionMiddleware` - sets `TenantId` in HttpContext after authentication
- `SessionAuthenticationMiddleware` - validates session cookies and sets HttpContext.User
- `ISessionService` - manages Redis-backed user sessions with sliding expiration

### Result Pattern

The `Result` and `Result<T>` types (Common.Core) replace exceptions for expected failure paths:

```csharp
public enum ErrorType { NotFound, Validation, Conflict, Unauthorized, Forbidden, Internal }

// Usage
var result = await GetOrderAsync(id);
if (!result.IsSuccess)
{
    return result.ErrorType switch
    {
        ErrorType.NotFound => Results.NotFound(result.ErrorMessage),
        ErrorType.Validation => Results.BadRequest(result.ErrorMessage),
        _ => Results.Problem(result.ErrorMessage)
    };
}
return Results.Ok(result.Value);
```

All HTTP error responses use ProblemDetails (RFC 9457) via `GlobalExceptionHandler`.

### Endpoint Organization

**MANDATORY PATTERN**: Every endpoint group implements `IEndpointGroup` and is auto-discovered.

```csharp
// src/GymnasticsPlatform.Api/Endpoints/OrderEndpoints.cs
public sealed class OrderEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");
        group.MapGet("/", ListOrders).RequireAuthorization();
        group.MapPost("/", CreateOrder).RequireAuthorization();
    }

    private static async Task<IResult> ListOrders(...) { }
    private static async Task<IResult> CreateOrder(...) { }
}
```

- **NEVER** add `MapGroup()` or endpoint calls to `Program.cs`
- `app.MapEndpoints()` in Program.cs auto-discovers all `IEndpointGroup` implementations
- One endpoint group per file in `src/GymnasticsPlatform.Api/Endpoints/`

### Domain Entities

Domain entities use:
- **Private setters** and **private parameterless constructors** (for EF Core)
- **Static factory methods** (`Create()`) for instantiation with validation
- **Immutability** - properties set once in factory method, modified only via domain methods
- **IMultiTenant** implementation for tenant-scoped entities

```csharp
public sealed class Club : IMultiTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private Club() { } // EF Core

    public static Club Create(Guid tenantId, string name, Guid ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Club name is required", nameof(name));

        return new Club
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            OwnerUserId = ownerUserId
        };
    }
}
```

### No Repository Pattern

**DO NOT** create repository interfaces over EF Core. `DbContext` is already a Unit of Work + Repository pattern.

```csharp
// CORRECT - inject DbContext directly
public sealed class OrderService(AuthDbContext db)
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct) =>
        db.Orders.FindAsync([id], ct).AsTask();
}

// WRONG - generic repository wrapping EF
public interface IRepository<T> { Task<T?> GetByIdAsync(Guid id); }
```

### Testing Strategy

**Integration tests first** using `WebApplicationFactory` + `Testcontainers`:
- Use real PostgreSQL via `Testcontainers.PostgreSql` (NEVER `UseInMemoryDatabase`)
- Use real Redis via `Testcontainers.Redis` for session testing
- Test full HTTP pipeline (serialization, middleware, auth, database)
- Shared fixtures (`IClassFixture<T>`) for expensive setup (containers)

**Authentication in Tests**:
- Tests run with `ASPNETCORE_ENVIRONMENT=Test` which disables authentication middleware
- Use `TestAuthenticationHandler` for authenticated test scenarios
- Session-based auth skipped in test environment - use claims-based testing
- Entra ID integration tested separately via E2E tests with real OAuth flow

**Test naming**: `MethodName_Scenario_ExpectedResult`
- Example: `CreateOrder_DuplicateSku_ReturnsConflict`

**AAA pattern** with blank lines separating Arrange, Act, Assert.

**Frontend tests** use Vitest + React Testing Library + Happy-DOM.

**E2E tests** use Playwright with Page Object Model pattern (see `tests/e2e/PAGE_OBJECT_STANDARDS.md`).

## Key Files & Locations

### Backend Structure
```
src/
├── Common/
│   ├── Common.Core/              # Result, ITenantContext, IMultiTenant
│   └── Common.Contracts/         # Integration events
├── Modules/
│   ├── Auth/                     # Auth module (Onion Architecture)
│   │   ├── Auth.Domain/          # Entities: UserProfile, Club, ClubInvite, Role
│   │   ├── Auth.Application/     # Services: IUserTenantService, IRoleService, IAuthenticationProvider
│   │   ├── Auth.Infrastructure/  # AuthDbContext, Persistence, Services
│   │   │   └── Services/
│   │   │       └── ExternalIdAuthenticationProvider.cs  # Entra ID integration
│   │   └── Auth.Api/             # (Module endpoints registered to composition root)
│   ├── Training/                 # Training module (RAG, skills, programmes)
│   │   ├── Training.Domain/      # Entities: Skill, Programme, VectorSearchResult
│   │   ├── Training.Application/ # Services: IEmbeddingService, ILlmService, IProgrammeService
│   │   ├── Training.Infrastructure/  # TrainingDbContext, Ollama, CouchDB, Vector search
│   │   └── Training.Api/         # (Module endpoints)
│   └── Sessions/                 # Sessions module (future)
└── GymnasticsPlatform.Api/       # Composition root
    ├── Program.cs                # DI registration, middleware pipeline
    ├── Endpoints/                # All IEndpointGroup implementations
    ├── Middleware/               # SessionAuthenticationMiddleware, TenantResolutionMiddleware
    ├── Services/                 # ISessionService, SessionService (Redis-backed)
    ├── Authorization/            # TenantRoleAuthorizationHandler
    └── IEndpointGroup.cs         # Auto-discovery interface
```

### Frontend Structure
```
frontend/
├── user-portal/                  # React SPA
│   └── src/
│       ├── App.tsx               # Main app with auth
│       ├── config/               # OAuth configuration
│       └── main.tsx              # Entry point
└── admin-portal/                 # React SPA
    └── src/
        ├── App.tsx
        ├── config/               # OAuth configuration
        └── main.tsx
```

### Testing Structure
```
tests/
├── Auth.Domain.Tests/            # Domain logic unit tests
├── Auth.Infrastructure.Tests/    # Infrastructure integration tests
├── Common.Core.Tests/            # Common library tests
├── GymnasticsPlatform.Integration.Tests/  # Full-stack integration tests
└── e2e/                          # Playwright E2E tests
    ├── tests/                    # Test suites
    ├── pages/                    # Page Object Models
    └── helpers/                  # Utilities
```

## Authentication Flow

The application uses **OAuth 2.0 Authorization Code Flow** with Microsoft Entra External ID:

1. User clicks "Sign in" in React frontend
2. Frontend redirects to Entra ID authorization endpoint
3. User authenticates with Google (federated identity) or Microsoft personal account
4. Entra ID redirects back with authorization code
5. Backend exchanges code for access token via `ExternalIdAuthenticationProvider`
6. Backend creates session in Redis and sets `session_id` cookie
7. `SessionAuthenticationMiddleware` validates cookie on subsequent requests
8. Session has 30-minute absolute timeout, 15-minute sliding expiration

**Key Components**:
- `ExternalIdAuthenticationProvider` - OAuth token exchange with Entra ID
- `SessionService` - Redis-backed session management
- `SessionAuthenticationMiddleware` - Cookie validation and user principal setup
- Session cookies are `HttpOnly`, `Secure`, `SameSite=Lax`

## Onboarding Flow

New users complete a 3-choice onboarding flow (see `docs/ONBOARDING_FLOW.md` for full details):

1. **Create Club** - User creates club, becomes org owner, gets new club tenant ID
2. **Join Club** - User enters invite code, joins existing club, inherits club tenant ID
3. **Individual Mode** - User gets unique individual tenant ID

After onboarding, `ExternalIdAuthenticationProvider` updates the user's `tenant_id` extension attribute in Entra ID via Microsoft Graph API. User must sign out and back in to refresh the session with updated tenant context.

**Endpoints**: All in `OnboardingEndpoints.cs`
- `GET /api/onboarding/status` - Check if user needs onboarding
- `POST /api/onboarding/create-club` - Create club path
- `POST /api/onboarding/join-club` - Join club path
- `POST /api/onboarding/individual` - Individual mode path

## Authorization Policies

Defined in `Program.cs` with custom `TenantRoleAuthorizationHandler`:

- `AdminPolicy` - Requires `platform_admin` role (stored in UserRoleMapping)
- `ClubAdminPolicy` - Requires `ClubAdmin` tenant role
- `CoachPolicy` - Requires `Coach`, `ClubAdmin`, or `IndividualAdmin` tenant role
- `GymnastPolicy` - Requires `Gymnast`, `Coach`, `ClubAdmin`, or `IndividualAdmin` tenant role

Tenant roles are stored in `Auth.Domain.Entities.UserRoleMapping` and checked via `IRoleService`. Roles are NOT stored in Entra ID - they are managed in the application database for fine-grained tenant-scoped authorization.

## Important Conventions

1. **Always use `TimeProvider.System`** instead of `DateTime.Now` or `DateTime.UtcNow` for testability
2. **Always propagate `CancellationToken`** through async call chains
3. **Use `IHttpClientFactory`** for all HTTP clients (configured for Entra ID, Ollama, CouchDB)
4. **Async all the way** - no `.Result` or `.Wait()` except in Program.cs top-level
5. **Primary constructors** for dependency injection
6. **File-scoped namespaces** always
7. **Collection expressions** over constructor calls: `List<int> ids = [1, 2, 3];`
8. **Records for DTOs and value objects** with immutability
9. **`sealed` classes** unless designed for inheritance
10. **FluentValidation** for input validation at API boundaries
11. **Session cookies for auth** - OAuth tokens are exchanged for sessions, never sent to frontend
12. **Extension attributes in Entra ID** - Custom user data (like `tenant_id`) stored as extension attributes

## Documentation

- `README.md` - Project overview, quick start, architecture
- `docs/ONBOARDING_FLOW.md` - Detailed onboarding system documentation
- `docs/ENTRA_ID_SETUP.md` - Microsoft Entra External ID setup and Google federation
- `infrastructure/external-id/README.md` - Terraform and automated provisioning scripts
- `infrastructure/external-id/CI-CD-SETUP.md` - GitHub Actions secrets for Entra ID
- `frontend/README.md` - Frontend apps overview and authentication flow
- `tests/e2e/README.md` - E2E testing guide
- `tests/e2e/PAGE_OBJECT_STANDARDS.md` - Page Object Model conventions

## Training Module (RAG System)

The **Training** module implements a Retrieval-Augmented Generation (RAG) system for gymnastics skills and training programmes:

**Components**:
- **Vector Database**: pgvector extension in PostgreSQL for semantic search
- **Embedding Service**: Ollama (local) or Azure OpenAI for text embeddings
- **LLM Service**: Ollama (local) or Azure OpenAI for programme generation
- **Document Store**: CouchDB for storing raw programme JSON documents
- **Skills Catalog**: Pre-seeded database of gymnastics skills with difficulty ratings

**Key Files**:
- `Training.Domain/Entities/Skill.cs` - Skill entity with vector embedding
- `Training.Infrastructure/Services/VectorSearchService.cs` - Semantic search over skills
- `Training.Infrastructure/Services/ProgrammeBuilderService.cs` - LLM-powered programme generation
- `Training.Infrastructure/DocumentStore/CouchDbProgrammeDocumentStore.cs` - Document persistence

**Seeding Skills**:
```bash
# Seed skills catalog from JSON file
dotnet run --project src/GymnasticsPlatform.Api seed-skills /path/to/skills.json

# See docs/SKILLS_SEEDING.md for JSON format and examples
```

**Configuration**:
```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "LlmModel": "llama3"
  },
  "CouchDb": {
    "Url": "http://localhost:5984",
    "Username": "admin",
    "Password": "password",
    "DatabaseName": "programmes"
  }
}
```
