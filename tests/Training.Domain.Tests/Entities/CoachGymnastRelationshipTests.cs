using Training.Domain.Entities;

namespace Training.Domain.Tests.Entities;

public sealed class CoachGymnastRelationshipTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInstance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var gymnastId = Guid.NewGuid();

        // Act
        var relationship = CoachGymnastRelationship.Create(tenantId, coachId, gymnastId);

        // Assert
        Assert.NotNull(relationship);
        Assert.NotEqual(Guid.Empty, relationship.Id);
        Assert.Equal(tenantId, relationship.TenantId);
        Assert.Equal(coachId, relationship.CoachId);
        Assert.Equal(gymnastId, relationship.GymnastId);
        Assert.True(relationship.IsActive);
        Assert.True(relationship.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.Empty;
        var coachId = Guid.NewGuid();
        var gymnastId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            CoachGymnastRelationship.Create(tenantId, coachId, gymnastId));
    }

    [Fact]
    public void Create_WithEmptyCoachId_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var coachId = Guid.Empty;
        var gymnastId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            CoachGymnastRelationship.Create(tenantId, coachId, gymnastId));
    }

    [Fact]
    public void Create_WithEmptyGymnastId_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var gymnastId = Guid.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            CoachGymnastRelationship.Create(tenantId, coachId, gymnastId));
    }

    [Fact]
    public void Deactivate_ChangesIsActiveToFalse()
    {
        // Arrange
        var relationship = CoachGymnastRelationship.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        Assert.True(relationship.IsActive);

        // Act
        relationship.Deactivate();

        // Assert
        Assert.False(relationship.IsActive);
    }

    [Fact]
    public void Reactivate_ChangesIsActiveToTrue()
    {
        // Arrange
        var relationship = CoachGymnastRelationship.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        relationship.Deactivate();
        Assert.False(relationship.IsActive);

        // Act
        relationship.Reactivate();

        // Assert
        Assert.True(relationship.IsActive);
    }
}
