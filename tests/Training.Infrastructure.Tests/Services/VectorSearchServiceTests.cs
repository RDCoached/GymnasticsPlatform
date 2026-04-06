using Common.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pgvector.EntityFrameworkCore;
using Training.Application.Services;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;
using Training.Infrastructure.Services;
using Training.Infrastructure.Tests.Fixtures;

namespace Training.Infrastructure.Tests.Services;

public sealed class VectorSearchServiceTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _pgFixture;
    private readonly TrainingDbContext _dbContext;
    private readonly VectorSearchService _service;
    private readonly Guid _testTenantId;
    private readonly Guid _gymnastId1;
    private readonly Guid _gymnastId2;

    public VectorSearchServiceTests(PostgreSqlFixture pgFixture)
    {
        _pgFixture = pgFixture;
        _testTenantId = Guid.NewGuid();
        _gymnastId1 = Guid.NewGuid();
        _gymnastId2 = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_testTenantId);

        var options = new DbContextOptionsBuilder<TrainingDbContext>()
            .UseNpgsql(_pgFixture.ConnectionString, o => o.UseVector())
            .Options;

        _dbContext = new TrainingDbContext(options, tenantContext);
        _service = new VectorSearchService(_dbContext);
    }

    [Fact]
    public async Task SearchSimilarProgrammesAsync_WithGymnastScope_ReturnsOnlyGymnastProgrammes()
    {
        // Arrange
        var queryEmbedding = CreateEmbedding(seed: 1);
        await SeedTestDataAsync();

        // Act
        var results = await _service.SearchSimilarProgrammesAsync(
            queryEmbedding,
            maxResults: 5,
            gymnastId: _gymnastId1);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Equal(_gymnastId1, p.GymnastId));
        Assert.All(results, p => Assert.Equal(_testTenantId, p.TenantId));
    }

    [Fact]
    public async Task SearchSimilarProgrammesAsync_WithTenantScope_ReturnsAllTenantProgrammes()
    {
        // Arrange
        var queryEmbedding = CreateEmbedding(seed: 1);
        await SeedTestDataAsync();

        // Act
        var results = await _service.SearchSimilarProgrammesAsync(
            queryEmbedding,
            maxResults: 5,
            gymnastId: null); // Tenant-wide search

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, p => p.GymnastId == _gymnastId1);
        Assert.Contains(results, p => p.GymnastId == _gymnastId2);
        Assert.All(results, p => Assert.Equal(_testTenantId, p.TenantId));
    }

    [Fact]
    public async Task SearchSimilarProgrammesAsync_OrdersBySimilarity_MostSimilarFirst()
    {
        // Arrange
        var queryEmbedding = CreateEmbedding(seed: 1);
        await SeedTestDataAsync();

        // Act
        var results = await _service.SearchSimilarProgrammesAsync(
            queryEmbedding,
            maxResults: 5,
            gymnastId: null);

        // Assert
        Assert.NotEmpty(results);

        // Verify ordering: calculate cosine similarity for each result
        var similarities = results.Select(p => CalculateCosineSimilarity(queryEmbedding, p.EmbeddingVector!)).ToList();

        // Check that similarities are in descending order (most similar first)
        for (var i = 0; i < similarities.Count - 1; i++)
        {
            Assert.True(similarities[i] >= similarities[i + 1],
                $"Result {i} (similarity {similarities[i]}) should be >= result {i + 1} (similarity {similarities[i + 1]})");
        }
    }

    [Fact]
    public async Task SearchSimilarProgrammesAsync_WithMaxResults_LimitsResultCount()
    {
        // Arrange
        var queryEmbedding = CreateEmbedding(seed: 1);
        await SeedTestDataAsync();

        // Act
        var results = await _service.SearchSimilarProgrammesAsync(
            queryEmbedding,
            maxResults: 2,
            gymnastId: null);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchSimilarProgrammesAsync_OnlyIncludesActiveAndCompletedProgrammes()
    {
        // Arrange
        var queryEmbedding = CreateEmbedding(seed: 1);
        await SeedTestDataAsync();

        // Seed a Draft programme (should be excluded)
        var draftProgramme = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId1,
            Guid.NewGuid(),
            "couch-draft",
            "1-rev",
            "Draft Programme",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(2));
        draftProgramme.SetEmbedding(CreateEmbedding(seed: 1)); // Very similar embedding
        _dbContext.ProgrammeMetadata.Add(draftProgramme);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _service.SearchSimilarProgrammesAsync(
            queryEmbedding,
            maxResults: 10,
            gymnastId: _gymnastId1);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(
            p.Status == ProgrammeStatus.Active || p.Status == ProgrammeStatus.Completed,
            $"Expected Active or Completed, but got {p.Status}"));
        Assert.DoesNotContain(results, p => p.Status == ProgrammeStatus.Draft);
    }

    [Fact]
    public async Task SearchSimilarProgrammesAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var queryEmbedding = CreateEmbedding(seed: 1);

        // Act
        var results = await _service.SearchSimilarProgrammesAsync(
            queryEmbedding,
            maxResults: 5,
            gymnastId: null);

        // Assert
        Assert.Empty(results);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE programme_metadata CASCADE");
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // Seed programmes for gymnastId1 with varying similarity to seed 1
        var prog1 = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId1,
            Guid.NewGuid(),
            "couch-prog1",
            "1-abc",
            "Vault Training",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(2));
        prog1.SetEmbedding(CreateEmbedding(seed: 1)); // Very similar
        prog1.Activate();

        var prog2 = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId1,
            Guid.NewGuid(),
            "couch-prog2",
            "1-def",
            "Floor Routine",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(2));
        prog2.SetEmbedding(CreateEmbedding(seed: 5)); // Less similar
        prog2.Complete();

        var prog3 = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId1,
            Guid.NewGuid(),
            "couch-prog3",
            "1-ghi",
            "Beam Balance",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(2));
        prog3.SetEmbedding(CreateEmbedding(seed: 10)); // Even less similar
        prog3.Complete();

        // Seed programmes for gymnastId2
        var prog4 = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId2,
            Guid.NewGuid(),
            "couch-prog4",
            "1-jkl",
            "Rings Training",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(2));
        prog4.SetEmbedding(CreateEmbedding(seed: 2)); // Somewhat similar
        prog4.Activate();

        _dbContext.ProgrammeMetadata.AddRange(prog1, prog2, prog3, prog4);
        await _dbContext.SaveChangesAsync();
    }

    private static float[] CreateEmbedding(int seed)
    {
        var random = new Random(seed);
        var embedding = new float[384];
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
        }
        return embedding;
    }

    private static float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same length");

        var dotProduct = 0.0;
        var magnitudeA = 0.0;
        var magnitudeB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        return (float)(dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB)));
    }
}
