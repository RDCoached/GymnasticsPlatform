using Training.Domain.Entities;
using Training.Domain.Enums;

namespace Training.Domain.Tests.Entities;

public sealed class SkillSectionTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInstance()
    {
        // Arrange
        var skillId = Guid.NewGuid();
        var section = GymnasticSection.Bars;

        // Act
        var skillSection = SkillSection.Create(skillId, section);

        // Assert
        Assert.NotNull(skillSection);
        Assert.NotEqual(Guid.Empty, skillSection.Id);
        Assert.Equal(skillId, skillSection.SkillId);
        Assert.Equal(section, skillSection.Section);
        Assert.True(skillSection.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Create_WithEmptySkillId_ThrowsArgumentException()
    {
        // Arrange
        var emptySkillId = Guid.Empty;
        var section = GymnasticSection.Floor;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SkillSection.Create(emptySkillId, section));
    }

    [Fact]
    public void Create_WithAllSections_CreatesCorrectly()
    {
        // Arrange
        var skillId = Guid.NewGuid();
        var sections = new[]
        {
            GymnasticSection.Bars,
            GymnasticSection.Floor,
            GymnasticSection.Beam,
            GymnasticSection.RangeVault,
            GymnasticSection.StrengthConditioning
        };

        // Act & Assert
        foreach (var section in sections)
        {
            var skillSection = SkillSection.Create(skillId, section);
            Assert.Equal(section, skillSection.Section);
        }
    }
}
