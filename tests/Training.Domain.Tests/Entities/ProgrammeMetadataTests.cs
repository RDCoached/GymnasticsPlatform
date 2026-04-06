using Training.Domain.Entities;
using Training.Domain.Enums;

namespace Training.Domain.Tests.Entities;

public sealed class ProgrammeMetadataTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInstance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var gymnastId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var couchDbDocId = "programme-abc123";
        var couchDbRev = "1-xyz789";
        var title = "Summer Vault Program";
        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddMonths(2);

        // Act
        var metadata = ProgrammeMetadata.Create(
            tenantId,
            gymnastId,
            coachId,
            couchDbDocId,
            couchDbRev,
            title,
            startDate,
            endDate);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotEqual(Guid.Empty, metadata.Id);
        Assert.Equal(tenantId, metadata.TenantId);
        Assert.Equal(gymnastId, metadata.GymnastId);
        Assert.Equal(coachId, metadata.CoachId);
        Assert.Equal(couchDbDocId, metadata.CouchDbDocId);
        Assert.Equal(couchDbRev, metadata.CouchDbRev);
        Assert.Equal(title, metadata.Title);
        Assert.Equal(ProgrammeStatus.Draft, metadata.Status);
        Assert.Equal(startDate, metadata.StartDate);
        Assert.Equal(endDate, metadata.EndDate);
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var gymnastId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var couchDbDocId = "programme-abc123";
        var couchDbRev = "1-xyz789";
        var emptyTitle = "";
        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddMonths(2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ProgrammeMetadata.Create(
                tenantId,
                gymnastId,
                coachId,
                couchDbDocId,
                couchDbRev,
                emptyTitle,
                startDate,
                endDate));
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var gymnastId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var couchDbDocId = "programme-abc123";
        var couchDbRev = "1-xyz789";
        var title = "Summer Vault Program";
        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddDays(-1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ProgrammeMetadata.Create(
                tenantId,
                gymnastId,
                coachId,
                couchDbDocId,
                couchDbRev,
                title,
                startDate,
                endDate));
    }

    [Fact]
    public void Activate_WhenDraft_ChangesStatusToActive()
    {
        // Arrange
        var metadata = CreateValidMetadata();
        Assert.Equal(ProgrammeStatus.Draft, metadata.Status);

        // Act
        metadata.Activate();

        // Assert
        Assert.Equal(ProgrammeStatus.Active, metadata.Status);
    }

    [Fact]
    public void Complete_WhenActive_ChangesStatusToCompleted()
    {
        // Arrange
        var metadata = CreateValidMetadata();
        metadata.Activate();
        Assert.Equal(ProgrammeStatus.Active, metadata.Status);

        // Act
        metadata.Complete();

        // Assert
        Assert.Equal(ProgrammeStatus.Completed, metadata.Status);
    }

    [Fact]
    public void Archive_ChangesStatusToArchived()
    {
        // Arrange
        var metadata = CreateValidMetadata();

        // Act
        metadata.Archive();

        // Assert
        Assert.Equal(ProgrammeStatus.Archived, metadata.Status);
    }

    [Fact]
    public void UpdateCouchDbRev_UpdatesRevisionValue()
    {
        // Arrange
        var metadata = CreateValidMetadata();
        var originalRev = metadata.CouchDbRev;
        var newRev = "2-abc456";

        // Act
        metadata.UpdateCouchDbRev(newRev);

        // Assert
        Assert.Equal(newRev, metadata.CouchDbRev);
        Assert.NotEqual(originalRev, metadata.CouchDbRev);
    }

    [Fact]
    public void SetEmbedding_WithValidVector_UpdatesEmbeddingVector()
    {
        // Arrange
        var metadata = CreateValidMetadata();
        var embedding = new float[384];
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = i * 0.01f;
        }

        // Act
        metadata.SetEmbedding(embedding);

        // Assert
        Assert.NotNull(metadata.EmbeddingVector);
        Assert.Equal(384, metadata.EmbeddingVector.Length);
        Assert.Equal(embedding, metadata.EmbeddingVector);
    }

    [Fact]
    public void SetEmbedding_WithInvalidDimensions_ThrowsArgumentException()
    {
        // Arrange
        var metadata = CreateValidMetadata();
        var invalidEmbedding = new float[100]; // Wrong dimension

        // Act & Assert
        Assert.Throws<ArgumentException>(() => metadata.SetEmbedding(invalidEmbedding));
    }

    private static ProgrammeMetadata CreateValidMetadata()
    {
        return ProgrammeMetadata.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "programme-test123",
            "1-abc",
            "Test Programme",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1));
    }
}
