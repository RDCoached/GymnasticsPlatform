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
        var inviteType = InviteType.Coach;
        var maxUses = 10;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;

        // Act
        var invite = ClubInvite.Create(clubId, inviteType, maxUses, expiresAt, null, null, clock);

        // Assert
        invite.Should().NotBeNull();
        invite.Id.Should().NotBeEmpty();
        invite.ClubId.Should().Be(clubId);
        invite.InviteType.Should().Be(inviteType);
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
        var invite1 = ClubInvite.Create(clubId, InviteType.Coach, maxUses, expiresAt, null, null, clock);
        var invite2 = ClubInvite.Create(clubId, InviteType.Coach, maxUses, expiresAt, null, null, clock);

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
        var invite1 = ClubInvite.Create(clubId, InviteType.Gymnast, maxUses, expiresAt, null, null, clock);
        var invite2 = ClubInvite.Create(clubId, InviteType.Gymnast, maxUses, expiresAt, null, null, clock);

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
        var act = () => ClubInvite.Create(clubId, InviteType.Coach, invalidMaxUses, expiresAt, null, null, clock);

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
        var act = () => ClubInvite.Create(clubId, InviteType.Gymnast, maxUses, expiresAt, null, null, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*expiresAt*");
    }

    [Theory]
    [InlineData(InviteType.Coach)]
    [InlineData(InviteType.Gymnast)]
    public void Create_AcceptsAllInviteTypes(InviteType inviteType)
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var maxUses = 10;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;

        // Act
        var invite = ClubInvite.Create(clubId, inviteType, maxUses, expiresAt, null, null, clock);

        // Assert
        invite.InviteType.Should().Be(inviteType);
    }

    [Fact]
    public void Create_WithDescription_StoresDescription()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var description = "Invite for Level 3 coaches";
        var clock = TimeProvider.System;

        // Act
        var invite = ClubInvite.Create(clubId, InviteType.Coach, 10, DateTimeOffset.UtcNow.AddDays(7), description, null, clock);

        // Assert
        invite.Description.Should().Be(description);
    }

    [Fact]
    public void Create_WithNullDescription_StoresNull()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var clock = TimeProvider.System;

        // Act
        var invite = ClubInvite.Create(clubId, InviteType.Gymnast, 10, DateTimeOffset.UtcNow.AddDays(7), null, null, clock);

        // Assert
        invite.Description.Should().BeNull();
    }

    [Fact]
    public void MarkAsUsed_IncrementsTimesUsed()
    {
        // Arrange
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            InviteType.Coach,
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            null,
            null,
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
            InviteType.Gymnast,
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            null,
            null,
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
            InviteType.Coach,
            maxUses: 2,
            DateTimeOffset.UtcNow.AddDays(7),
            null,
            null,
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
            InviteType.Gymnast,
            maxUses: 10,
            DateTimeOffset.UtcNow.AddSeconds(1),
            null,
            null,
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
            InviteType.Coach,
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            null,
            null,
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
            InviteType.Gymnast,
            maxUses: 10,
            DateTimeOffset.UtcNow.AddSeconds(1),
            null,
            null,
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
            InviteType.Coach,
            maxUses: 10,
            DateTimeOffset.UtcNow.AddDays(7),
            null,
            null,
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
            InviteType.Gymnast,
            maxUses: 2,
            DateTimeOffset.UtcNow.AddDays(7),
            null,
            null,
            TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);
        invite.MarkAsUsed(TimeProvider.System);

        // Act
        var isAtMax = invite.IsAtMaxUses();

        // Assert
        isAtMax.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmail_ForcesMaxUsesToOne()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;

        // Act
        var act = () => ClubInvite.Create(
            clubId,
            InviteType.Coach,
            maxUses: 10,
            expiresAt,
            null,
            "test@example.com",
            clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email-specific invites must have MaxUses = 1*");
    }

    [Fact]
    public void Create_WithEmail_SetsSentAtToCurrentTime()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var clock = TimeProvider.System;
        var now = clock.GetUtcNow();

        // Act
        var invite = ClubInvite.Create(
            clubId,
            InviteType.Coach,
            maxUses: 1,
            expiresAt,
            null,
            "test@example.com",
            clock);

        // Assert
        invite.SentAt.Should().NotBeNull();
        invite.SentAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithEmail_StoresEmail()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var email = "newuser@example.com";
        var clock = TimeProvider.System;

        // Act
        var invite = ClubInvite.Create(
            clubId,
            InviteType.Gymnast,
            maxUses: 1,
            DateTimeOffset.UtcNow.AddDays(7),
            "Welcome aboard",
            email,
            clock);

        // Assert
        invite.Email.Should().Be(email);
    }

    [Fact]
    public void Create_WithoutEmail_LeavesEmailNull()
    {
        // Arrange
        var clubId = Guid.NewGuid();
        var clock = TimeProvider.System;

        // Act
        var invite = ClubInvite.Create(
            clubId,
            InviteType.Gymnast,
            maxUses: 50,
            DateTimeOffset.UtcNow.AddDays(7),
            "Public invite",
            email: null,
            clock);

        // Assert
        invite.Email.Should().BeNull();
        invite.SentAt.Should().BeNull();
    }

    [Fact]
    public void IsSingleUse_WithEmail_ReturnsTrue()
    {
        // Arrange
        var clock = TimeProvider.System;
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            InviteType.Gymnast,
            maxUses: 1,
            clock.GetUtcNow().AddDays(7),
            null,
            "test@example.com",
            clock);

        // Act
        var isSingleUse = invite.IsSingleUse();

        // Assert
        isSingleUse.Should().BeTrue();
    }

    [Fact]
    public void IsSingleUse_WithoutEmail_ReturnsFalse()
    {
        // Arrange
        var clock = TimeProvider.System;
        var invite = ClubInvite.Create(
            Guid.NewGuid(),
            InviteType.Gymnast,
            maxUses: 50,
            clock.GetUtcNow().AddDays(7),
            "Public invite",
            email: null,
            clock);

        // Act
        var isSingleUse = invite.IsSingleUse();

        // Assert
        isSingleUse.Should().BeFalse();
    }
}
