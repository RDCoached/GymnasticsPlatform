using Training.Domain.Entities;

namespace Training.Domain.Tests.Entities;

public sealed class SkillTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInstance()
    {
        // Arrange
        var title = "Handstand";
        var description = "Hold handstand position for 30 seconds with proper form";
        var effectivenessRating = 4;
        var createdByTenantId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();
        var imageUrl = "https://example.com/handstand.jpg";

        // Act
        var skill = Skill.Create(
            title,
            description,
            effectivenessRating,
            createdByTenantId,
            createdByUserId,
            imageUrl);

        // Assert
        Assert.NotNull(skill);
        Assert.NotEqual(Guid.Empty, skill.Id);
        Assert.Equal(title, skill.Title);
        Assert.Equal(description, skill.Description);
        Assert.Equal(effectivenessRating, skill.EffectivenessRating);
        Assert.Equal(createdByTenantId, skill.CreatedByTenantId);
        Assert.Equal(createdByUserId, skill.CreatedByUserId);
        Assert.Equal(imageUrl, skill.ImageUrl);
        Assert.Equal(0, skill.UsageCount);
        Assert.Null(skill.EmbeddingVector);
    }

    [Fact]
    public void Create_WithoutImageUrl_ReturnsInstanceWithNullImageUrl()
    {
        // Arrange
        var title = "Push-up";
        var description = "Standard push-up exercise";
        var effectivenessRating = 3;
        var createdByTenantId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();

        // Act
        var skill = Skill.Create(
            title,
            description,
            effectivenessRating,
            createdByTenantId,
            createdByUserId);

        // Assert
        Assert.NotNull(skill);
        Assert.Null(skill.ImageUrl);
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var emptyTitle = "";
        var description = "Some description";
        var effectivenessRating = 3;
        var createdByTenantId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Skill.Create(
                emptyTitle,
                description,
                effectivenessRating,
                createdByTenantId,
                createdByUserId));
    }

    [Fact]
    public void Create_WithEmptyDescription_ThrowsArgumentException()
    {
        // Arrange
        var title = "Handstand";
        var emptyDescription = "";
        var effectivenessRating = 3;
        var createdByTenantId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Skill.Create(
                title,
                emptyDescription,
                effectivenessRating,
                createdByTenantId,
                createdByUserId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Create_WithInvalidRating_ThrowsArgumentException(int invalidRating)
    {
        // Arrange
        var title = "Handstand";
        var description = "Some description";
        var createdByTenantId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Skill.Create(
                title,
                description,
                invalidRating,
                createdByTenantId,
                createdByUserId));
    }

    [Fact]
    public void IncrementUsageCount_IncreasesUsageByOne()
    {
        // Arrange
        var skill = CreateValidSkill();
        var initialCount = skill.UsageCount;

        // Act
        skill.IncrementUsageCount();

        // Assert
        Assert.Equal(initialCount + 1, skill.UsageCount);
    }

    [Fact]
    public void IncrementUsageCount_MultipleTimes_IncreasesCorrectly()
    {
        // Arrange
        var skill = CreateValidSkill();

        // Act
        skill.IncrementUsageCount();
        skill.IncrementUsageCount();
        skill.IncrementUsageCount();

        // Assert
        Assert.Equal(3, skill.UsageCount);
    }

    [Fact]
    public void DecrementUsageCount_DecreasesUsageByOne()
    {
        // Arrange
        var skill = CreateValidSkill();
        skill.IncrementUsageCount();
        skill.IncrementUsageCount();
        var initialCount = skill.UsageCount;

        // Act
        skill.DecrementUsageCount();

        // Assert
        Assert.Equal(initialCount - 1, skill.UsageCount);
    }

    [Fact]
    public void DecrementUsageCount_WhenZero_RemainsZero()
    {
        // Arrange
        var skill = CreateValidSkill();
        Assert.Equal(0, skill.UsageCount);

        // Act
        skill.DecrementUsageCount();

        // Assert
        Assert.Equal(0, skill.UsageCount);
    }

    [Fact]
    public void SetEmbedding_WithValidVector_UpdatesEmbeddingVector()
    {
        // Arrange
        var skill = CreateValidSkill();
        var embedding = new float[384];
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = i * 0.01f;
        }

        // Act
        skill.SetEmbedding(embedding);

        // Assert
        Assert.NotNull(skill.EmbeddingVector);
        Assert.Equal(384, skill.EmbeddingVector.Length);
        Assert.Equal(embedding, skill.EmbeddingVector);
    }

    [Fact]
    public void SetEmbedding_WithInvalidDimensions_ThrowsArgumentException()
    {
        // Arrange
        var skill = CreateValidSkill();
        var invalidEmbedding = new float[100]; // Wrong dimension

        // Act & Assert
        Assert.Throws<ArgumentException>(() => skill.SetEmbedding(invalidEmbedding));
    }

    [Fact]
    public void Update_WithValidData_UpdatesProperties()
    {
        // Arrange
        var skill = CreateValidSkill();
        var newTitle = "Advanced Handstand";
        var newDescription = "Hold handstand with one arm for 15 seconds";
        var newRating = 5;
        var newImageUrl = "https://example.com/advanced-handstand.jpg";

        // Act
        skill.Update(newTitle, newDescription, newRating, newImageUrl);

        // Assert
        Assert.Equal(newTitle, skill.Title);
        Assert.Equal(newDescription, skill.Description);
        Assert.Equal(newRating, skill.EffectivenessRating);
        Assert.Equal(newImageUrl, skill.ImageUrl);
    }

    [Fact]
    public void Update_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var skill = CreateValidSkill();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            skill.Update("", "Some description", 3, null));
    }

    [Fact]
    public void Update_WithInvalidRating_ThrowsArgumentException()
    {
        // Arrange
        var skill = CreateValidSkill();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            skill.Update("Title", "Description", 0, null));
    }

    private static Skill CreateValidSkill()
    {
        return Skill.Create(
            "Test Skill",
            "Test skill description",
            3,
            Guid.NewGuid(),
            Guid.NewGuid());
    }
}
