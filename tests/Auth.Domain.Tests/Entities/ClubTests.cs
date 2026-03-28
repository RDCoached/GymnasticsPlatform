using Auth.Domain.Entities;
using FluentAssertions;

namespace Auth.Domain.Tests.Entities;

public sealed class ClubTests
{
    [Fact]
    public void Create_WithValidData_ReturnsClubWithCorrectValues()
    {
        // Arrange
        var name = "Elite Gymnastics Club";
        var ownerUserId = "user-123";
        var clock = TimeProvider.System;

        // Act
        var club = Club.Create(name, ownerUserId, clock);

        // Assert
        club.Should().NotBeNull();
        club.Id.Should().NotBeEmpty();
        club.Name.Should().Be(name);
        club.OwnerUserId.Should().Be(ownerUserId);
        club.TenantId.Should().NotBeEmpty();
        club.TenantId.Should().NotBe(Guid.Empty);
        club.CreatedAt.Should().BeCloseTo(clock.GetUtcNow(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_GeneratesUniqueTenantIds()
    {
        // Arrange
        var name = "Test Club";
        var ownerUserId = "user-123";
        var clock = TimeProvider.System;

        // Act
        var club1 = Club.Create(name, ownerUserId, clock);
        var club2 = Club.Create(name, ownerUserId, clock);

        // Assert
        club1.TenantId.Should().NotBe(club2.TenantId);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var name = "Test Club";
        var ownerUserId = "user-123";
        var clock = TimeProvider.System;

        // Act
        var club1 = Club.Create(name, ownerUserId, clock);
        var club2 = Club.Create(name, ownerUserId, clock);

        // Assert
        club1.Id.Should().NotBe(club2.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithEmptyName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var ownerUserId = "user-123";
        var clock = TimeProvider.System;

        // Act
        var act = () => Club.Create(invalidName!, ownerUserId, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithEmptyOwnerUserId_ThrowsArgumentException(string? invalidOwnerUserId)
    {
        // Arrange
        var name = "Elite Gymnastics Club";
        var clock = TimeProvider.System;

        // Act
        var act = () => Club.Create(name, invalidOwnerUserId!, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ownerUserId*");
    }

    [Fact]
    public void Club_ImplementsIMultiTenant()
    {
        // Arrange
        var club = Club.Create("Test Club", "user-123", TimeProvider.System);

        // Assert
        club.Should().BeAssignableTo<Common.Core.IMultiTenant>();
        ((Common.Core.IMultiTenant)club).TenantId.Should().Be(club.TenantId);
    }

    [Theory]
    [InlineData("Elite Gymnastics")]
    [InlineData("Springfield Athletic Club")]
    [InlineData("YMCA")]
    [InlineData("The Gymnastics Academy of Excellence")]
    public void Create_AcceptsVariousNameFormats(string name)
    {
        // Act
        var club = Club.Create(name, "user-123", TimeProvider.System);

        // Assert
        club.Name.Should().Be(name);
    }
}
