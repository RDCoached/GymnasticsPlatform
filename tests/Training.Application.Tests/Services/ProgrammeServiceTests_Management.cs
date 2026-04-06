using NSubstitute;
using Training.Application.Services;
using Training.Application.Tests.Fixtures;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;
using Training.Infrastructure.Services;
using Common.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;
using Training.Infrastructure.Configuration;

namespace Training.Application.Tests.Services;

[Collection("Database")]
public sealed class ProgrammeServiceTests_Management : IAsyncLifetime
{
    private readonly PostgreSqlFixture _pgFixture;
    private readonly TrainingDbContext _dbContext;
    private readonly IProgrammeDocumentStore _documentStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ProgrammeService _service;
    private readonly Guid _testTenantId;
    private readonly Guid _gymnastId;
    private readonly Guid _coachId;

    public ProgrammeServiceTests_Management(PostgreSqlFixture pgFixture, OllamaFixture ollamaFixture)
    {
        _pgFixture = pgFixture;
        _testTenantId = Guid.NewGuid();
        _gymnastId = Guid.NewGuid();
        _coachId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_testTenantId);

        var options = new DbContextOptionsBuilder<TrainingDbContext>()
            .UseNpgsql(_pgFixture.ConnectionString, o => o.UseVector())
            .Options;

        _dbContext = new TrainingDbContext(options, tenantContext);

        _documentStore = Substitute.For<IProgrammeDocumentStore>();
        _embeddingService = Substitute.For<IEmbeddingService>();

        _service = new ProgrammeService(_dbContext, _documentStore, _embeddingService);
    }

    [Fact]
    public async Task ActivateAsync_WithValidProgramme_SetsStatusToActive()
    {
        // Arrange
        var programme = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId,
            _coachId,
            "couch-123",
            "1-abc",
            "Test Programme",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1));
        _dbContext.ProgrammeMetadata.Add(programme);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ActivateAsync(programme.Id);

        // Assert
        var updated = await _dbContext.ProgrammeMetadata.FindAsync(programme.Id);
        Assert.NotNull(updated);
        Assert.Equal(ProgrammeStatus.Active, updated.Status);
    }

    [Fact]
    public async Task ActivateAsync_DeactivatesExistingActiveProgramme()
    {
        // Arrange - Create existing active programme
        var existingProgramme = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId,
            _coachId,
            "couch-old",
            "1-old",
            "Old Programme",
            DateTimeOffset.UtcNow.AddMonths(-2),
            DateTimeOffset.UtcNow.AddMonths(-1));
        existingProgramme.Activate();
        _dbContext.ProgrammeMetadata.Add(existingProgramme);

        var newProgramme = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId,
            _coachId,
            "couch-new",
            "1-new",
            "New Programme",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1));
        _dbContext.ProgrammeMetadata.Add(newProgramme);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ActivateAsync(newProgramme.Id);

        // Assert
        var oldProg = await _dbContext.ProgrammeMetadata.FindAsync(existingProgramme.Id);
        var newProg = await _dbContext.ProgrammeMetadata.FindAsync(newProgramme.Id);

        Assert.NotNull(oldProg);
        Assert.NotNull(newProg);
        Assert.Equal(ProgrammeStatus.Completed, oldProg.Status);
        Assert.Equal(ProgrammeStatus.Active, newProg.Status);
    }

    [Fact]
    public async Task CompleteAsync_WithActiveProgramme_SetsStatusToCompleted()
    {
        // Arrange
        var programme = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId,
            _coachId,
            "couch-123",
            "1-abc",
            "Test Programme",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1));
        programme.Activate();
        _dbContext.ProgrammeMetadata.Add(programme);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.CompleteAsync(programme.Id);

        // Assert
        var updated = await _dbContext.ProgrammeMetadata.FindAsync(programme.Id);
        Assert.NotNull(updated);
        Assert.Equal(ProgrammeStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task ArchiveAsync_WithCompletedProgramme_SetsStatusToArchived()
    {
        // Arrange
        var programme = ProgrammeMetadata.Create(
            _testTenantId,
            _gymnastId,
            _coachId,
            "couch-123",
            "1-abc",
            "Test Programme",
            DateTimeOffset.UtcNow.AddMonths(-2),
            DateTimeOffset.UtcNow.AddMonths(-1));
        programme.Activate();
        programme.Complete();
        _dbContext.ProgrammeMetadata.Add(programme);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ArchiveAsync(programme.Id);

        // Assert
        var updated = await _dbContext.ProgrammeMetadata.FindAsync(programme.Id);
        Assert.NotNull(updated);
        Assert.Equal(ProgrammeStatus.Archived, updated.Status);
    }

    [Fact]
    public async Task ActivateAsync_WithNonExistentProgramme_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ActivateAsync(nonExistentId));
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
}
