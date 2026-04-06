using Common.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Pgvector.EntityFrameworkCore;
using Training.Application.Services;
using Training.Application.Tests.Fixtures;
using Training.Domain.Documents;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;
using Training.Infrastructure.Services;

namespace Training.Application.Tests.Services;

[Collection("Database")]
public sealed class ProgrammeBuilderServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _pgFixture;
    private readonly TrainingDbContext _dbContext;
    private readonly VectorSearchService _vectorSearchService;
    private readonly IProgrammeDocumentStore _documentStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILlmService _llmService;
    private readonly IProgrammeService _programmeService;
    private readonly ProgrammeBuilderService _service;
    private readonly Guid _testTenantId;
    private readonly Guid _coachId;

    public ProgrammeBuilderServiceTests(PostgreSqlFixture pgFixture, OllamaFixture ollamaFixture)
    {
        _pgFixture = pgFixture;
        _testTenantId = Guid.NewGuid();
        _coachId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_testTenantId);

        var options = new DbContextOptionsBuilder<TrainingDbContext>()
            .UseNpgsql(_pgFixture.ConnectionString, o => o.UseVector())
            .Options;

        _dbContext = new TrainingDbContext(options, tenantContext);
        _vectorSearchService = new VectorSearchService(_dbContext);

        // Mock dependencies
        _documentStore = Substitute.For<IProgrammeDocumentStore>();
        _embeddingService = Substitute.For<IEmbeddingService>();
        _llmService = Substitute.For<ILlmService>();
        _programmeService = Substitute.For<IProgrammeService>();

        _service = new ProgrammeBuilderService(
            _dbContext,
            _vectorSearchService,
            _documentStore,
            _embeddingService,
            _llmService,
            _programmeService,
            tenantContext);
    }

    [Fact]
    public async Task StartSessionAsync_WithValidGoals_CreatesSessionAndReturnssuggestion()
    {
        // Arrange
        var gymnastId = Guid.NewGuid();
        var goals = "Improve vault power and landing stability";

        // Setup mocks
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockEmbedding());

        _llmService.GenerateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<CancellationToken>())
            .Returns("Here's a 4-week vault training programme...");

        // Act
        var result = await _service.StartSessionAsync(gymnastId, goals, "gymnast");

        // Assert
        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotNull(result.Suggestion);
        Assert.Contains("vault", result.Suggestion, StringComparison.OrdinalIgnoreCase);

        // Verify session was saved
        var session = await _dbContext.ProgrammeBuilderSessions.FindAsync(result.SessionId);
        Assert.NotNull(session);
        Assert.Equal(gymnastId, session.GymnastId);
        Assert.Equal(goals, session.InitialGoals);
        Assert.Equal(SessionStatus.Active, session.Status);
    }

    [Fact]
    public async Task StartSessionAsync_WithSimilarProgrammes_IncludesThemInContext()
    {
        // Arrange
        var gymnastId = Guid.NewGuid();
        var goals = "Vault training";

        // Seed similar programme
        await SeedProgrammeWithEmbeddingAsync(gymnastId, "Vault Programme", CreateMockEmbedding());

        // Setup mocks
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockEmbedding());

        _documentStore.BulkGetAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns([CreateTestDocument("Vault Training Programme")]);

        string? capturedSystemPrompt = null;
        _llmService.GenerateAsync(
            Arg.Do<string>(x => capturedSystemPrompt = x),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<CancellationToken>())
            .Returns("Based on similar vault programmes...");

        // Act
        var result = await _service.StartSessionAsync(gymnastId, goals, "gymnast");

        // Assert
        Assert.NotNull(result.Suggestion);
        await _documentStore.Received(1).BulkGetAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContinueSessionAsync_WithExistingSession_AppendsToHistory()
    {
        // Arrange
        var gymnastId = Guid.NewGuid();
        var session = ProgrammeBuilderSession.Create(_testTenantId, _coachId, gymnastId, "Initial goals", "gymnast");
        session.AppendConversation("Initial message", "Initial response");
        _dbContext.ProgrammeBuilderSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockEmbedding());

        _llmService.GenerateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ConversationMessage>>(),
            Arg.Any<CancellationToken>())
            .Returns("Updated programme with your feedback...");

        // Act
        var result = await _service.ContinueSessionAsync(session.Id, "Can we add more plyometrics?");

        // Assert
        Assert.Equal(session.Id, result.SessionId);
        Assert.NotNull(result.Suggestion);

        // Verify conversation history was passed to LLM
        await _llmService.Received(1).GenerateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<ConversationMessage>>(h => h != null && h.Count >= 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptSessionAsync_WithValidSession_CreatesProgramme()
    {
        // Arrange
        var gymnastId = Guid.NewGuid();
        var session = ProgrammeBuilderSession.Create(_testTenantId, _coachId, gymnastId, "Test goals", "gymnast");
        session.AppendConversation("Create a vault programme", "Here's a programme with exercises...");
        _dbContext.ProgrammeBuilderSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var expectedProgrammeId = Guid.NewGuid();
        var mockMetadata = ProgrammeMetadata.Create(
            _testTenantId,
            gymnastId,
            _coachId,
            "couch-id",
            "rev",
            "Vault Programme",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1));
        typeof(ProgrammeMetadata).GetProperty("Id")!.SetValue(mockMetadata, expectedProgrammeId);

        _programmeService.CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>())
            .Returns(mockMetadata);

        // Act
        var programmeId = await _service.AcceptSessionAsync(session.Id);

        // Assert
        Assert.Equal(expectedProgrammeId, programmeId);

        // Verify session was completed
        var updatedSession = await _dbContext.ProgrammeBuilderSessions.FindAsync(session.Id);
        Assert.NotNull(updatedSession);
        Assert.Equal(SessionStatus.Completed, updatedSession.Status);
        Assert.Equal(programmeId, updatedSession.ResultingProgrammeId);

        // Verify programme was created
        await _programmeService.Received(1).CreateAsync(Arg.Any<ProgrammeDocument>(), Arg.Any<CancellationToken>());
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE programme_metadata CASCADE");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE programme_builder_sessions CASCADE");
        await _dbContext.DisposeAsync();
    }

    private async Task SeedProgrammeWithEmbeddingAsync(Guid gymnastId, string title, float[] embedding)
    {
        var programme = ProgrammeMetadata.Create(
            _testTenantId,
            gymnastId,
            _coachId,
            "couch-abc",
            "1-rev",
            title,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1));
        programme.SetEmbedding(embedding);
        programme.Activate();
        _dbContext.ProgrammeMetadata.Add(programme);
        await _dbContext.SaveChangesAsync();
    }

    private static float[] CreateMockEmbedding()
    {
        var embedding = new float[384];
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = i * 0.01f;
        }
        return embedding;
    }

    private ProgrammeDocument CreateTestDocument(string title)
    {
        return new ProgrammeDocument
        {
            Id = $"programme-{Guid.NewGuid()}",
            TenantId = _testTenantId,
            GymnastId = Guid.NewGuid(),
            CoachId = _coachId,
            Title = title,
            Goals = "Test goals",
            Content = new ProgrammeContent
            {
                Weeks =
                [
                    new WeekContent
                    {
                        WeekNumber = 1,
                        Focus = "Power development",
                        Exercises =
                        [
                            new ExerciseContent
                            {
                                Name = "Box jumps",
                                Sets = 4,
                                Reps = "10",
                                Notes = "Explosive power"
                            }
                        ]
                    }
                ]
            },
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddMonths(1)
        };
    }
}
