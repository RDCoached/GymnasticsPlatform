using Auth.Domain.Entities;
using FluentAssertions;

namespace Auth.Domain.Tests.Entities;

public sealed class ClubInviteTests
{
    [Fact]
    public void Create_WithValidData_ReturnsClubInviteWithCorrectValues()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var maxUses = 10;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;

        // Act
        var invite = ClubInvite.Create(clubId, maxUses, expiresAt, clock);

        // Assert
        invite.Should().NotBeNull();
        invite.Id.Should().NotBeEmpty();
        invite.ClubId.Should().Be(clubId);
        invite.Code.Should().NotBeNullOrWhiteSpace();
        invite.Code.Length.Should().BeGreaterThan(6);
        invite.MaxUses.Should().Be(maxUses);
        invite.TimesUsed.Should().Be(0);
        invite.ExpiresAt.Should().Be(expiresAt);
        invite.CreatedAt.Should().BeCloseTo(clock.GetUtcNow(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_GeneratesUniqueInviteCodes()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var maxUses = 10;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;

        // Act
        var invite1 = ClubInvite.Create(clubId, maxUses, expiresAt, clock);
        var invite2 = ClubInvite.Create(clubId, maxUses, expiresAt, clock);

        // Assert
        invite1.Code.Should().NotBe(invite2.Code);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var maxUses = 10;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;

        // Act
        var invite1 = ClubInvite.Create(clubId, maxUses, expiresAt, clock);
        var invite2 = ClubInvite.Create(clubId, maxUses, expiresAt, clock);

        // Assert
        invite1.Id.Should().NotBe(invite2.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Create_WithInvalidMaxUses_ThrowsArgumentException(int invalidMaxUses)
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;

        // Act
        var act = () => ClubInvite.Create(clubId, invalidMaxUses, expiresAt, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*maxUses*");
    }

    [Fact]
    public void Create_WithPastExpirationDate_ThrowsArgumentException()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var maxUses = 10;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        var clock = TimeProvider.System;

        // Act
        var act = () => ClubInvite.Create(clubId, maxUses, expiresAt, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*expiresAt*");
    }

    [Fact]
    public void MarkAsUsed_IncrementsTimesUsed()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            TimeProvider.System);

        // Act
        invite.MarkAsUsed(TimeProvider.System);

        // Assert
        invite.TimesUsed.Should().Be(1);
    }

    [Fact]
    public void MarkAsUsed_CanBeCalledMultipleTimes()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            TimeProvider.System);

        // Act
        invite.MarkAsUsed(TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);

        // Assert
        invite.TimesUsed.Should().Be(3);
    }

    [Fact]
    public void MarkAsUsed_WhenAtMaxUses_ThrowsInvalidOperationException()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 2,
            DateTimeOffset.UtcNow.AddDays(7),
            TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);

        // Act
        var act = () => invite.MarkAsUsed(TimeProvider.System);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*maximum*");
    }

    [Fact]
    public void MarkAsUsed_WhenExpired_ThrowsInvalidOperationException()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 10,
            DateTimeOffset.UtcNow.AddSeconds(1),
            TimeProvider.System);

        Thread.Sleep(1500);

        // Act
        var act = () => invite.MarkAsUsed(TimeProvider.System);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public void IsExpired_WhenNotExpired_ReturnsFalse()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            TimeProvider.System);

        // Act
        var isExpired = invite.IsExpired(TimeProvider.System.GetUtcNow());

        // Assert
        isExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpired_ReturnsTrue()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 10,
            DateTimeOffset.UtcNow.AddSeconds(1),
            TimeProvider.System);

        // Act - check expiration against a time 2 seconds in the future
        var isExpired = invite.IsExpired(DateTimeOffset.UtcNow.AddSeconds(2));

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public void IsAtMaxUses_WhenBelowMax_ReturnsFalse()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);

        // Act
        var isAtMax = invite.IsAtMaxUses();

        // Assert
        isAtMax.Should().BeFalse();
    }

    [Fact]
    public void IsAtMaxUses_WhenAtMax_ReturnsTrue()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            maxUses: 2,
            DateTimeOffset.UtcNow.AddDays(7),
            TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);

        // Act
        var isAtMax = invite.IsAtMaxUses();

        // Assert
        isAtMax.Should().BeTrue();
    }
}
