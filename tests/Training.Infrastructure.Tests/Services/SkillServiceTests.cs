using Common.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Pgvector.EntityFrameworkCore;
using Training.Application.Services;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;
using Training.Infrastructure.Services;
using Training.Infrastructure.Tests.Fixtures;

namespace Training.Infrastructure.Tests.Services;

public sealed class SkillServiceTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _pgFixture;
    private readonly TrainingDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly SkillService _service;
    private readonly Guid _testTenantId;
    private readonly Guid _testUserId;

    public SkillServiceTests(PostgreSqlFixture pgFixture)
    {
        _pgFixture = pgFixture;
        _testTenantId = Guid.NewGuid();
        _testUserId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_testTenantId);

        var options = new DbContextOptionsBuilder<TrainingDbContext>()
            .UseNpgsql(_pgFixture.ConnectionString, o => o.UseVector())
            .Options;

        _dbContext = new TrainingDbContext(options, tenantContext);

        _embeddingService = Substitute.For<IEmbeddingService>();
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateEmbedding(seed: 1));

        _service = new SkillService(_dbContext, _embeddingService);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsSuccessResult()
    {
        // Arrange
        var title = "Handstand";
        var description = "Hold handstand position for 30 seconds";
        var rating = 4;
        var sections = new[] { GymnasticSection.Floor, GymnasticSection.Bars }.ToList().AsReadOnly();

        // Act
        var result = await _service.CreateAsync(
            title,
            description,
            rating,
            sections,
            _testTenantId,
            _testUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(title, result.Value.Title);
        Assert.Equal(description, result.Value.Description);
        Assert.Equal(rating, result.Value.EffectivenessRating);
        Assert.Equal(_testTenantId, result.Value.CreatedByTenantId);
        Assert.Equal(_testUserId, result.Value.CreatedByUserId);
        Assert.NotNull(result.Value.EmbeddingVector);
        Assert.Equal(384, result.Value.EmbeddingVector.Length);

        await _embeddingService.Received(1).GenerateEmbeddingAsync(
            $"{title}\n{description}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithMultipleSections_CreatesSectionEntities()
    {
        // Arrange
        var sections = new[] { GymnasticSection.Floor, GymnasticSection.Bars, GymnasticSection.Beam }
            .ToList()
            .AsReadOnly();

        // Act
        var result = await _service.CreateAsync(
            "Test Skill",
            "Test description",
            3,
            sections,
            _testTenantId,
            _testUserId);

        // Assert
        Assert.True(result.IsSuccess);
        var skillFromDb = await _dbContext.Skills
            .Include(s => s.Sections)
            .FirstAsync(s => s.Id == result.Value!.Id);

        Assert.Equal(3, skillFromDb.Sections.Count);
        Assert.Contains(skillFromDb.Sections, ss => ss.Section == GymnasticSection.Floor);
        Assert.Contains(skillFromDb.Sections, ss => ss.Section == GymnasticSection.Bars);
        Assert.Contains(skillFromDb.Sections, ss => ss.Section == GymnasticSection.Beam);
    }

    [Fact]
    public async Task UpdateAsync_WithNewSections_ReplacesOldSections()
    {
        // Arrange
        var skill = await CreateTestSkillAsync(
            sections: new[] { GymnasticSection.Floor }.ToList().AsReadOnly());

        var newSections = new[] { GymnasticSection.Bars, GymnasticSection.Beam }
            .ToList()
            .AsReadOnly();

        // Act
        var result = await _service.UpdateAsync(
            skill.Id,
            "Updated Title",
            "Updated description",
            5,
            newSections);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedSkill = await _dbContext.Skills
            .Include(s => s.Sections)
            .FirstAsync(s => s.Id == skill.Id);

        Assert.Equal(2, updatedSkill.Sections.Count);
        Assert.Contains(updatedSkill.Sections, ss => ss.Section == GymnasticSection.Bars);
        Assert.Contains(updatedSkill.Sections, ss => ss.Section == GymnasticSection.Beam);
        Assert.DoesNotContain(updatedSkill.Sections, ss => ss.Section == GymnasticSection.Floor);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingSkill_ReturnsSkill()
    {
        // Arrange
        var skill = await CreateTestSkillAsync();

        // Act
        var result = await _service.GetByIdAsync(skill.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(skill.Id, result.Value!.Id);
        Assert.Equal(skill.Title, result.Value.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNotFoundError()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetByIdAsync(nonExistentId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task SearchAsync_WithQuery_ReturnsRelevantSkills()
    {
        // Arrange
        await SeedTestSkillsAsync();

        // Mock embedding for query
        _embeddingService.GenerateEmbeddingAsync("handstand", Arg.Any<CancellationToken>())
            .Returns(CreateEmbedding(seed: 1));

        // Act
        var result = await _service.SearchAsync("handstand", maxResults: 5);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!);
        Assert.All(result.Value, r => Assert.InRange(r.SimilarityScore, 0, 1));
    }

    [Fact]
    public async Task SearchAsync_WithSectionFilter_ReturnsOnlyMatchingSection()
    {
        // Arrange
        await CreateTestSkillAsync(
            title: "Floor Exercise",
            sections: new[] { GymnasticSection.Floor }.ToList().AsReadOnly());

        await CreateTestSkillAsync(
            title: "Bars Routine",
            sections: new[] { GymnasticSection.Bars }.ToList().AsReadOnly());

        // Act
        var result = await _service.SearchAsync(
            "exercise",
            maxResults: 10,
            section: GymnasticSection.Floor);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!);
        Assert.All(result.Value, r =>
            Assert.Contains(r.Skill.Sections, ss => ss.Section == GymnasticSection.Floor));
    }

    [Fact]
    public async Task DeleteAsync_WhenInUse_ReturnsConflictError()
    {
        // Arrange
        var skill = await CreateTestSkillAsync();
        skill.IncrementUsageCount();
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteAsync(skill.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Contains("in use", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotInUse_RemovesSkill()
    {
        // Arrange
        var skill = await CreateTestSkillAsync();
        Assert.Equal(0, skill.UsageCount);

        // Act
        var result = await _service.DeleteAsync(skill.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var deletedSkill = await _dbContext.Skills.FindAsync(skill.Id);
        Assert.Null(deletedSkill);
    }

    [Fact]
    public async Task ListAsync_FilteredBySection_ReturnsMatchingSkills()
    {
        // Arrange
        await CreateTestSkillAsync(
            title: "Floor Skill 1",
            sections: new[] { GymnasticSection.Floor }.ToList().AsReadOnly());

        await CreateTestSkillAsync(
            title: "Floor Skill 2",
            sections: new[] { GymnasticSection.Floor }.ToList().AsReadOnly());

        await CreateTestSkillAsync(
            title: "Bars Skill",
            sections: new[] { GymnasticSection.Bars }.ToList().AsReadOnly());

        // Act
        var result = await _service.ListAsync(
            section: GymnasticSection.Floor,
            pageNumber: 1,
            pageSize: 10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Skills.Count);
        Assert.All(result.Value.Skills, s =>
            Assert.Contains(s.Sections, ss => ss.Section == GymnasticSection.Floor));
    }

    [Fact]
    public async Task ListAsync_FilteredByMinRating_ReturnsMatchingSkills()
    {
        // Arrange
        await CreateTestSkillAsync(title: "Low Rating", rating: 2);
        await CreateTestSkillAsync(title: "High Rating", rating: 5);

        // Act
        var result = await _service.ListAsync(
            minRating: 4,
            pageNumber: 1,
            pageSize: 10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Skills);
        Assert.Equal("High Rating", result.Value.Skills[0].Title);
    }

    [Fact]
    public async Task ListAsync_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            await CreateTestSkillAsync(title: $"Skill {i}");
        }

        // Act
        var result = await _service.ListAsync(
            pageNumber: 2,
            pageSize: 2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Skills.Count);
        Assert.Equal(5, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(2, result.Value.PageSize);
        Assert.Equal(3, result.Value.TotalPages);
    }

    [Fact]
    public async Task IncrementUsageAsync_IncreasesCount()
    {
        // Arrange
        var skill = await CreateTestSkillAsync();
        Assert.Equal(0, skill.UsageCount);

        // Act
        var result = await _service.IncrementUsageAsync(skill.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedSkill = await _dbContext.Skills.FindAsync(skill.Id);
        Assert.Equal(1, updatedSkill!.UsageCount);
    }

    [Fact]
    public async Task DecrementUsageAsync_DecreasesCount()
    {
        // Arrange
        var skill = await CreateTestSkillAsync();
        skill.IncrementUsageCount();
        skill.IncrementUsageCount();
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DecrementUsageAsync(skill.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedSkill = await _dbContext.Skills.FindAsync(skill.Id);
        Assert.Equal(1, updatedSkill!.UsageCount);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE skill_sections CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE skills CASCADE");
        await _dbContext.DisposeAsync();
    }

    private async Task<Skill> CreateTestSkillAsync(
        string title = "Test Skill",
        string description = "Test description",
        int rating = 3,
        IReadOnlyList<GymnasticSection>? sections = null)
    {
        sections ??= new[] { GymnasticSection.Floor }.ToList().AsReadOnly();

        var result = await _service.CreateAsync(
            title,
            description,
            rating,
            sections,
            _testTenantId,
            _testUserId);

        return result.Value!;
    }

    private async Task SeedTestSkillsAsync()
    {
        await CreateTestSkillAsync(
            title: "Handstand",
            description: "Hold handstand for 30 seconds",
            sections: new[] { GymnasticSection.Floor, GymnasticSection.Bars }.ToList().AsReadOnly());

        await CreateTestSkillAsync(
            title: "Back Flip",
            description: "Complete back flip with landing",
            sections: new[] { GymnasticSection.Floor }.ToList().AsReadOnly());

        await CreateTestSkillAsync(
            title: "Beam Walk",
            description: "Walk across balance beam",
            sections: new[] { GymnasticSection.Beam }.ToList().AsReadOnly());
    }

    private static float[] CreateEmbedding(int seed)
    {
        var random = new Random(seed);
        var embedding = new float[384];
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }
        return embedding;
    }
}
