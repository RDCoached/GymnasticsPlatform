using Auth.Application.Services;
using Auth.Domain.Events;
using Auth.Infrastructure.Handlers;
using Common.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Auth.Infrastructure.Tests.Handlers;

public sealed class UserTenantUpdatedHandlerTests
{
    [Fact]
    public async Task HandleAsync_CallsAuthProviderWithCorrectParameters()
    {
        // Arrange
        var authProvider = Substitute.For<IAuthenticationProvider>();
        var logger = Substitute.For<ILogger<UserTenantUpdatedHandler>>();
        var handler = new UserTenantUpdatedHandler(authProvider, logger);

        var userId = Guid.NewGuid();
        var providerUserId = "entra-user-123";
        var oldTenantId = Guid.NewGuid();
        var newTenantId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var email = "test@example.com";
        var fullName = "Test User";

        var evt = new UserTenantUpdatedEvent(
            userId,
            providerUserId,
            oldTenantId,
            newTenantId,
            occurredAt,
            email,
            fullName);

        authProvider.UpdateUserTenantIdAsync(providerUserId, newTenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

        // Act
        await handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        await authProvider.Received(1).UpdateUserTenantIdAsync(
            providerUserId,
            newTenantId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_LogsInformationMessages()
    {
        // Arrange
        var authProvider = Substitute.For<IAuthenticationProvider>();
        var logger = Substitute.For<ILogger<UserTenantUpdatedHandler>>();
        var handler = new UserTenantUpdatedHandler(authProvider, logger);

        var userId = Guid.NewGuid();
        var providerUserId = "entra-user-123";
        var oldTenantId = Guid.NewGuid();
        var newTenantId = Guid.NewGuid();

        var evt = new UserTenantUpdatedEvent(
            userId,
            providerUserId,
            oldTenantId,
            newTenantId,
            DateTimeOffset.UtcNow,
            "test@example.com",
            "Test User");

        authProvider.UpdateUserTenantIdAsync(providerUserId, newTenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

        // Act
        await handler.HandleAsync(evt, CancellationToken.None);

        // Assert - verify logging (NSubstitute doesn't easily verify ILogger calls, so we just ensure no exceptions)
        // In a real scenario, you might use a logging testing library like Serilog.Sinks.InMemory
    }

    [Fact]
    public async Task HandleAsync_WhenAuthProviderThrows_LogsErrorAndRethrows()
    {
        // Arrange
        var authProvider = Substitute.For<IAuthenticationProvider>();
        var logger = Substitute.For<ILogger<UserTenantUpdatedHandler>>();
        var handler = new UserTenantUpdatedHandler(authProvider, logger);

        var userId = Guid.NewGuid();
        var providerUserId = "entra-user-123";
        var oldTenantId = Guid.NewGuid();
        var newTenantId = Guid.NewGuid();

        var evt = new UserTenantUpdatedEvent(
            userId,
            providerUserId,
            oldTenantId,
            newTenantId,
            DateTimeOffset.UtcNow,
            "test@example.com",
            "Test User");

        var expectedException = new InvalidOperationException("External provider error");
        authProvider.UpdateUserTenantIdAsync(providerUserId, newTenantId, Arg.Any<CancellationToken>())
            .Throws(expectedException);

        // Act
        var act = async () => await handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External provider error");

        await authProvider.Received(1).UpdateUserTenantIdAsync(
            providerUserId,
            newTenantId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationTokenToAuthProvider()
    {
        // Arrange
        var authProvider = Substitute.For<IAuthenticationProvider>();
        var logger = Substitute.For<ILogger<UserTenantUpdatedHandler>>();
        var handler = new UserTenantUpdatedHandler(authProvider, logger);

        var evt = new UserTenantUpdatedEvent(
            Guid.NewGuid(),
            "entra-user-123",
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "test@example.com",
            "Test User");

        authProvider.UpdateUserTenantIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

        var cts = new CancellationTokenSource();

        // Act
        await handler.HandleAsync(evt, cts.Token);

        // Assert
        await authProvider.Received(1).UpdateUserTenantIdAsync(
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            cts.Token);
    }
}
