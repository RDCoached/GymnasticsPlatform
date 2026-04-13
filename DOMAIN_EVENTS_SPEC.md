# Domain Events Implementation with Wolverine - Specification

## Overview

Implement domain events using Wolverine to decouple domain logic from infrastructure concerns. This will improve maintainability, testability, and adherence to Domain-Driven Design (DDD) principles.

## Problem Statement

Currently, domain operations directly call infrastructure services, creating tight coupling. For example:

```csharp
// UserTenantService.cs - Current implementation
userProfile.UpdateTenant(newTenantId);
await _db.SaveChangesAsync(ct);

// Directly calls infrastructure service - tight coupling!
await _authProvider.UpdateUserTenantIdAsync(providerUserId, newTenantId, ct);
```

**Issues:**
- Domain logic coupled to infrastructure (IAuthenticationProvider)
- Hard to test domain logic in isolation
- Violates Single Responsibility Principle
- Can't easily add side effects (logging, notifications, analytics)

## Solution: Domain Events with Wolverine

Use domain events to decouple domain operations from their side effects:

```csharp
// Domain raises event
userProfile.UpdateTenant(newTenantId); // Raises UserTenantUpdatedEvent
await _db.SaveChangesAsync(ct);        // Commits + publishes events

// Infrastructure handles event asynchronously
public class UserTenantUpdatedHandler
{
    public async Task Handle(UserTenantUpdatedEvent evt)
    {
        await _authProvider.UpdateUserTenantIdAsync(evt.ProviderUserId, evt.NewTenantId);
    }
}
```

## Why Wolverine?

**Chosen over:**
- ~~MediatR~~ (license concerns with recent changes)
- ~~MassTransit~~ (overkill for in-process messaging)
- ~~RabbitMQ~~ (external dependency, infrastructure complexity)

**Wolverine advantages:**
- In-process messaging (no external broker needed)
- Built-in transactional outbox pattern
- Retry policies and error handling
- Message routing and scheduling
- Seamless .NET integration
- MIT license

## Architecture

### Event Flow

```
1. Domain Entity
   └─> Raises Domain Event (in-memory)
   
2. Domain Event Interceptor (EF Core SaveChangesInterceptor)
   └─> Collects events before SaveChanges
   └─> Publishes to Wolverine after successful commit
   
3. Wolverine Message Bus
   └─> Routes to appropriate handler(s)
   
4. Event Handler(s)
   └─> Execute side effects (update Entra ID, send email, log, etc.)
```

### Project Structure

```
src/
├── Common/
│   └── Common.Core/
│       └── DomainEvents/
│           ├── IDomainEvent.cs           # Marker interface
│           ├── IHasDomainEvents.cs       # Entity interface
│           └── EntityBase.cs             # Base entity with event collection
│
├── Modules/
│   └── Auth/
│       ├── Auth.Domain/
│       │   ├── Entities/
│       │   │   └── UserProfile.cs        # Inherits EntityBase, raises events
│       │   └── Events/
│       │       └── UserTenantUpdatedEvent.cs
│       │
│       └── Auth.Infrastructure/
│           └── Handlers/
│               └── UserTenantUpdatedHandler.cs
│
└── GymnasticsPlatform.Api/
    └── Infrastructure/
        └── DomainEventInterceptor.cs    # EF Core interceptor
```

## Implementation Steps

### Step 1: Add Wolverine Package

```bash
dotnet add src/GymnasticsPlatform.Api/GymnasticsPlatform.Api.csproj package WolverineHttp
```

### Step 2: Create Domain Event Infrastructure

**File: `src/Common/Common.Core/DomainEvents/IDomainEvent.cs`**
```csharp
namespace Common.Core.DomainEvents;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent something that happened in the domain.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
```

**File: `src/Common/Common.Core/DomainEvents/IHasDomainEvents.cs`**
```csharp
namespace Common.Core.DomainEvents;

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
```

**File: `src/Common/Common.Core/DomainEvents/EntityBase.cs`**
```csharp
namespace Common.Core.DomainEvents;

public abstract class EntityBase : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

### Step 3: Create Domain Event

**File: `src/Modules/Auth/Auth.Domain/Events/UserTenantUpdatedEvent.cs`**
```csharp
using Common.Core.DomainEvents;

namespace Auth.Domain.Events;

public sealed record UserTenantUpdatedEvent(
    Guid UserId,
    string ProviderUserId,
    Guid OldTenantId,
    Guid NewTenantId,
    DateTimeOffset OccurredAt,
    string Email,
    string FullName
) : IDomainEvent;
```

### Step 4: Update Domain Entity

**File: `src/Modules/Auth/Auth.Domain/Entities/UserProfile.cs`**

Update `UserProfile` to:
1. Inherit from `EntityBase`
2. Raise `UserTenantUpdatedEvent` when tenant changes

```csharp
using Common.Core;
using Common.Core.DomainEvents;
using Auth.Domain.Events;

namespace Auth.Domain.Entities;

public sealed class UserProfile : EntityBase, IMultiTenant
{
    // ... existing properties ...

    public void UpdateTenant(Guid newTenantId, TimeProvider clock)
    {
        if (newTenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(newTenantId));

        var oldTenantId = TenantId;
        TenantId = newTenantId;

        // Raise domain event
        RaiseEvent(new UserTenantUpdatedEvent(
            UserId: Id,
            ProviderUserId: ProviderUserId,
            OldTenantId: oldTenantId,
            NewTenantId: newTenantId,
            OccurredAt: clock.GetUtcNow(),
            Email: Email,
            FullName: FullName
        ));
    }
}
```

### Step 5: Create EF Core Domain Event Interceptor

**File: `src/GymnasticsPlatform.Api/Infrastructure/DomainEventInterceptor.cs`**

```csharp
using Common.Core.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wolverine;

namespace GymnasticsPlatform.Api.Infrastructure;

/// <summary>
/// EF Core interceptor that publishes domain events after successful SaveChanges.
/// This ensures events are only published if the transaction commits successfully.
/// </summary>
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IMessageBus _messageBus;

    public DomainEventInterceptor(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task PublishDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Get all entities with domain events
        var entitiesWithEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        // Collect all events
        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Clear events from entities
        entitiesWithEvents.ForEach(e => e.ClearDomainEvents());

        // Publish each event to Wolverine
        foreach (var domainEvent in domainEvents)
        {
            await _messageBus.PublishAsync(domainEvent, cancellationToken);
        }
    }
}
```

### Step 6: Create Event Handler

**File: `src/Modules/Auth/Auth.Infrastructure/Handlers/UserTenantUpdatedHandler.cs`**

```csharp
using Auth.Application.Services;
using Auth.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Handlers;

/// <summary>
/// Handles UserTenantUpdatedEvent by updating the user's tenant_id in Entra ID.
/// This ensures future JWT tokens contain the correct tenant_id claim.
/// </summary>
public sealed class UserTenantUpdatedHandler
{
    private readonly IAuthenticationProvider _authProvider;
    private readonly ILogger<UserTenantUpdatedHandler> _logger;

    public UserTenantUpdatedHandler(
        IAuthenticationProvider authProvider,
        ILogger<UserTenantUpdatedHandler> logger)
    {
        _authProvider = authProvider;
        _logger = logger;
    }

    public async Task Handle(UserTenantUpdatedEvent evt, CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing UserTenantUpdatedEvent: User {UserId} tenant changed from {OldTenant} to {NewTenant}",
            evt.UserId, evt.OldTenantId, evt.NewTenantId);

        try
        {
            // Update tenant_id extension attribute in Entra ID
            await _authProvider.UpdateUserTenantIdAsync(evt.ProviderUserId, evt.NewTenantId, ct);

            _logger.LogInformation(
                "Successfully updated tenant_id in Entra ID for user {UserId}",
                evt.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to update tenant_id in Entra ID for user {UserId}",
                evt.UserId);
            throw; // Let Wolverine handle retry
        }
    }
}
```

### Step 7: Configure Wolverine

**File: `src/GymnasticsPlatform.Api/Program.cs`**

Add after `var builder = WebApplication.CreateBuilder(args);`:

```csharp
using Wolverine;

// Add Wolverine for domain events
builder.Host.UseWolverine(opts =>
{
    // In-process messaging (no external broker)
    opts.LocalQueue("domain-events")
        .Sequential(); // Process events in order

    // Auto-discover handlers in all loaded assemblies
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.Discovery.IncludeAssembly(typeof(Auth.Application.Services.IUserTenantService).Assembly);

    // Configure policies
    opts.Policies.AutoApplyTransactions();
    opts.Policies.OnException<TimeoutException>().RetryTimes(3);
});
```

Register the interceptor:

```csharp
// Register Domain Event Interceptor
builder.Services.AddSingleton<DomainEventInterceptor>();

// Add DbContext with Domain Event Interceptor
builder.Services.AddDbContext<AuthDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider.GetRequiredService<DomainEventInterceptor>();
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(interceptor);
});
```

### Step 8: Update Service Layer

**File: `src/Modules/Auth/Auth.Infrastructure/Services/UserTenantService.cs`**

Remove direct call to `IAuthenticationProvider`:

```csharp
public async Task UpdateUserTenantAsync(
    string providerUserId,
    Guid newTenantId,
    string? email = null,
    string? fullName = null,
    CancellationToken ct = default)
{
    var userProfile = await _db.UserProfiles
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(u => u.ProviderUserId == providerUserId, ct);

    if (userProfile is not null)
    {
        // Update existing profile's tenant (raises domain event)
        var oldTenantId = userProfile.TenantId;
        userProfile.UpdateTenant(newTenantId, _clock);

        _logger.LogInformation(
            "Updated user {UserId} tenant from {OldTenant} to {NewTenant}",
            providerUserId, oldTenantId, newTenantId);
    }
    else
    {
        // Create new profile...
        userProfile = UserProfile.Create(
            newTenantId,
            providerUserId,
            email,
            fullName,
            _clock.GetUtcNow());

        _db.UserProfiles.Add(userProfile);

        _logger.LogInformation(
            "Created user profile for {UserId} in tenant {TenantId}",
            providerUserId, newTenantId);
    }

    await _db.SaveChangesAsync(ct);
    // Event is published automatically by DomainEventInterceptor

    // REMOVED: Direct infrastructure call
    // await _authProvider.UpdateUserTenantIdAsync(providerUserId, newTenantId, ct);
}
```

## Testing Requirements

### Unit Tests

**File: `tests/Auth.Domain.Tests/UserProfileTests.cs`**

Test that domain events are raised:

```csharp
[Fact]
public void UpdateTenant_RaisesDomainEvent()
{
    // Arrange
    var clock = new FakeTimeProvider();
    var userProfile = UserProfile.Create(
        Guid.NewGuid(),
        "provider-123",
        "test@example.com",
        "Test User",
        clock.GetUtcNow());
    
    var newTenantId = Guid.NewGuid();

    // Act
    userProfile.UpdateTenant(newTenantId, clock);

    // Assert
    userProfile.DomainEvents.Should().HaveCount(1);
    var evt = userProfile.DomainEvents.First().Should().BeOfType<UserTenantUpdatedEvent>().Subject;
    evt.NewTenantId.Should().Be(newTenantId);
    evt.ProviderUserId.Should().Be("provider-123");
}
```

### Integration Tests

**File: `tests/GymnasticsPlatform.Integration.Tests/DomainEvents/UserTenantUpdatedHandlerTests.cs`**

Test the full flow:

```csharp
[Fact]
public async Task UpdateTenant_PublishesEventAndUpdatesEntraId()
{
    // Arrange
    var mockAuthProvider = Substitute.For<IAuthenticationProvider>();
    // ... setup test

    // Act
    await userTenantService.UpdateUserTenantAsync(providerUserId, newTenantId);
    await Task.Delay(100); // Wait for async event processing

    // Assert
    await mockAuthProvider.Received(1).UpdateUserTenantIdAsync(providerUserId, newTenantId, Arg.Any<CancellationToken>());
}
```

## Acceptance Criteria

- [ ] Wolverine package installed and configured
- [ ] `EntityBase` and domain event infrastructure created in Common.Core
- [ ] `UserTenantUpdatedEvent` defined in Auth.Domain
- [ ] `UserProfile.UpdateTenant()` raises domain event
- [ ] `DomainEventInterceptor` publishes events after SaveChanges
- [ ] `UserTenantUpdatedHandler` updates Entra ID
- [ ] Direct infrastructure call removed from `UserTenantService`
- [ ] Unit tests verify events are raised
- [ ] Integration tests verify end-to-end flow
- [ ] All existing tests still pass
- [ ] System builds without errors
- [ ] Manual testing: tenant update still updates Entra ID correctly

## Blog Post

Create a blog post documenting:
1. Why domain events matter
2. The problem we solved
3. Why we chose Wolverine
4. Implementation walkthrough
5. Benefits gained
6. Code examples

**Target file:** `blogs/implementing-domain-events-with-wolverine.md`

## Branch Strategy

Work should be done in the existing worktree:
- **Location:** `/Users/rdcoached/domain-events-wolverine`
- **Branch:** `domain-events-wolverine`

## References

- [Wolverine Documentation](https://wolverine.netlify.app/)
- [Domain Events Pattern (Martin Fowler)](https://martinfowler.com/eaaDev/DomainEvent.html)
- [EF Core Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)
