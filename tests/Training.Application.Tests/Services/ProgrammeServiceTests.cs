using Common.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pgvector.EntityFrameworkCore;
using Training.Application.Services;
using Training.Application.Tests.Fixtures;
using Training.Domain.Documents;
using Training.Domain.Enums;
using Training.Infrastructure.Configuration;
using Training.Infrastructure.Persistence;
using Training.Infrastructure.Services;

namespace Training.Application.Tests.Services;

[Collection("Database")]
public sealed class ProgrammeServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _pgFixture;
    private readonly OllamaFixture _ollamaFixture;
    private readonly TrainingDbContext _dbContext;
    private readonly IProgrammeDocumentStore _documentStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ProgrammeService _service;
    private readonly Guid _testTenantId;

    public ProgrammeServiceTests(PostgreSqlFixture pgFixture, OllamaFixture ollamaFixture)
    {
        _pgFixture = pgFixture;
        _ollamaFixture = ollamaFixture;
        _testTenantId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_testTenantId);

        var options = new DbContextOptionsBuilder<TrainingDbContext>()
            .UseNpgsql(_pgFixture.ConnectionString, o => o.UseVector())
            .Options;

        _dbContext = new TrainingDbContext(options, tenantContext);

        _documentStore = Substitute.For<IProgrammeDocumentStore>();

        // Use real Ollama embedding service instead of mock
        var httpClient = new HttpClient();
        var ollamaSettings = Options.Create(new OllamaSettings
        {
            BaseUrl = _ollamaFixture.BaseUrl,
            EmbeddingModel = "all-minilm:l6-v2",
            TimeoutSeconds = 120
        });
        _embeddingService = new OllamaEmbeddingService(httpClient, ollamaSettings);

        var skillService = Substitute.For<ISkillService>();
        _service = new ProgrammeService(_dbContext, _documentStore, _embeddingService, skillService);
    }

    [Fact]
    public async Task CreateAsync_WithValidDocument_CreatesBothStores()
    {
        // Arrange
        var document = CreateTestDocument();
        _documentStore
            .CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns(("programme-abc123", "1-xyz789"));

        // Act
        var result = await _service.CreateAsync(document);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(_testTenantId, result.TenantId);
        Assert.Equal(document.GymnastId, result.GymnastId);
        Assert.Equal(document.CoachId, result.CoachId);
        Assert.Equal(document.Title, result.Title);
        Assert.Equal("programme-abc123", result.CouchDbDocId);
        Assert.Equal("1-xyz789", result.CouchDbRev);
        Assert.Equal(ProgrammeStatus.Draft, result.Status);
        Assert.NotNull(result.EmbeddingVector);
        Assert.Equal(384, result.EmbeddingVector.Length);
        // Verify embedding values are in valid range for all-minilm:l6-v2
        Assert.All(result.EmbeddingVector, value => Assert.True(value >= -1.0f && value <= 1.0f));

        await _documentStore.Received(1).CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>());
        // Note: Can't verify call count on real embedding service, but embedding generation is verified by the assertions above

        var savedMetadata = await _dbContext.ProgrammeMetadata.FirstOrDefaultAsync();
        Assert.NotNull(savedMetadata);
        Assert.Equal(result.Id, savedMetadata.Id);
    }

    [Fact]
    public async Task GetAsync_ExistingProgramme_ReturnsBothMetadataAndDocument()
    {
        // Arrange
        var document = CreateTestDocument();
        _documentStore
            .CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns(("programme-abc123", "1-xyz789"));

        var metadata = await _service.CreateAsync(document);

        _documentStore
            .GetAsync("programme-abc123", Arg.Any<CancellationToken>())
            .Returns(document);

        // Act
        var result = await _service.GetAsync(metadata.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(metadata.Id, result.Value.Metadata.Id);
        Assert.Equal(document.Title, result.Value.Document.Title);
        Assert.Equal(document.Goals, result.Value.Document.Goals);

        await _documentStore.Received(1).GetAsync("programme-abc123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_NonExistentProgramme_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ExistingProgramme_UpdatesBothStores()
    {
        // Arrange
        var document = CreateTestDocument();
        _documentStore
            .CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns(("programme-abc123", "1-xyz789"));

        var metadata = await _service.CreateAsync(document);

        document.Title = "Updated Title";
        document.Goals = "Updated Goals";
        document.Id = "programme-abc123";
        document.Rev = "1-xyz789";

        _documentStore
            .UpdateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns("2-newrev");

        // Act
        var result = await _service.UpdateAsync(metadata.Id, document);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(metadata.Id, result.Id);
        Assert.Equal("2-newrev", result.CouchDbRev);

        await _documentStore.Received(1).UpdateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>());

        var updatedMetadata = await _dbContext.ProgrammeMetadata.FindAsync(metadata.Id);
        Assert.NotNull(updatedMetadata);
        Assert.Equal("2-newrev", updatedMetadata.CouchDbRev);
    }

    [Fact]
    public async Task DeleteAsync_ExistingProgramme_DeletesFromBothStores()
    {
        // Arrange
        var document = CreateTestDocument();
        _documentStore
            .CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns(("programme-abc123", "1-xyz789"));

        var metadata = await _service.CreateAsync(document);

        _documentStore
            .DeleteAsync("programme-abc123", "1-xyz789", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _service.DeleteAsync(metadata.Id);

        // Assert
        Assert.True(result);

        await _documentStore.Received(1).DeleteAsync("programme-abc123", "1-xyz789", Arg.Any<CancellationToken>());

        var deletedMetadata = await _dbContext.ProgrammeMetadata.FindAsync(metadata.Id);
        Assert.Null(deletedMetadata);
    }

    [Fact]
    public async Task ActivateAsync_WhenOtherProgrammeIsActive_DeactivatesOldAndActivatesNew()
    {
        // Arrange
        var gymnastId = Guid.NewGuid();

        var doc1 = CreateTestDocument();
        doc1.GymnastId = gymnastId;
        _documentStore
            .CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns(x => ($"programme-{Guid.NewGuid()}", "1-abc"));

        var programme1 = await _service.CreateAsync(doc1);
        await _service.ActivateAsync(programme1.Id);

        var doc2 = CreateTestDocument();
        doc2.GymnastId = gymnastId;
        var programme2 = await _service.CreateAsync(doc2);

        // Act
        var result = await _service.ActivateAsync(programme2.Id);

        // Assert
        Assert.Equal(ProgrammeStatus.Active, result.Status);

        var oldProgramme = await _dbContext.ProgrammeMetadata.FindAsync(programme1.Id);
        Assert.NotNull(oldProgramme);
        Assert.Equal(ProgrammeStatus.Completed, oldProgramme.Status);
    }

    [Fact]
    public async Task ListByGymnastAsync_ReturnsAllProgrammesForGymnast()
    {
        // Arrange
        var gymnastId = Guid.NewGuid();

        var doc1 = CreateTestDocument();
        doc1.GymnastId = gymnastId;
        doc1.Title = "Programme 1";

        var doc2 = CreateTestDocument();
        doc2.GymnastId = gymnastId;
        doc2.Title = "Programme 2";

        var doc3 = CreateTestDocument();
        doc3.GymnastId = Guid.NewGuid(); // Different gymnast
        doc3.Title = "Programme 3";

        _documentStore
            .CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns(x => ($"programme-{Guid.NewGuid()}", "1-abc"));

        await _service.CreateAsync(doc1);
        await _service.CreateAsync(doc2);
        await _service.CreateAsync(doc3);

        // Act
        var result = await _service.ListByGymnastAsync(gymnastId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal(gymnastId, p.GymnastId));
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE programme_metadata CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE coach_gymnast_relationships CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE programme_builder_sessions CASCADE");
        await _dbContext.DisposeAsync();
    }

    private ProgrammeDocument CreateTestDocument()
    {
        return new ProgrammeDocument
        {
            TenantId = _testTenantId,
            GymnastId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Test Programme",
            Goals = "Test goals for the programme",
            Content = new ProgrammeContent
            {
                Weeks =
                [
                    new WeekContent
                    {
                        WeekNumber = 1,
                        Focus = "Foundation skills",
                        Exercises =
                        [
                            new ExerciseContent
                            {
                                Name = "Handstand practice",
                                Sets = 3,
                                Reps = "30s",
                                Notes = "Focus on form"
                            }
                        ],
                        Notes = "Start with basics"
                    }
                ],
                Progressions = ["Week 1: 20s", "Week 4: 40s"],
                GeneralNotes = "Track progress weekly"
            },
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddMonths(2)
        };
    }

}
