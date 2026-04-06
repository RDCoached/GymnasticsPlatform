using Microsoft.Extensions.Options;
using Training.Domain.Documents;
using Training.Infrastructure.Configuration;
using Training.Infrastructure.DocumentStore;
using Training.Infrastructure.Tests.Fixtures;

namespace Training.Infrastructure.Tests.DocumentStore;

[Collection("CouchDb")]
public sealed class CouchDbProgrammeDocumentStoreTests : IClassFixture<CouchDbFixture>
{
    private readonly CouchDbFixture _fixture;
    private readonly CouchDbProgrammeDocumentStore _store;

    public CouchDbProgrammeDocumentStoreTests(CouchDbFixture fixture)
    {
        _fixture = fixture;

        var httpClient = new HttpClient();
        var settings = Options.Create(new CouchDbSettings
        {
            ServerUrl = _fixture.ServerUrl,
            DatabaseName = _fixture.Database,
            Username = _fixture.User,
            Password = _fixture.Pass
        });

        _store = new CouchDbProgrammeDocumentStore(httpClient, settings);
    }

    [Fact]
    public async Task CreateAsync_WithValidDocument_ReturnsDocIdAndRev()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var (docId, rev) = await _store.CreateAsync(document);

        // Assert
        Assert.NotNull(docId);
        Assert.NotEmpty(docId);
        Assert.NotNull(rev);
        Assert.NotEmpty(rev);
        Assert.StartsWith("programme-", docId);
        Assert.Equal(docId, document.Id);
        Assert.Equal(rev, document.Rev);
    }

    [Fact]
    public async Task GetAsync_ExistingDocument_ReturnsDocument()
    {
        // Arrange
        var document = CreateTestDocument();
        var (docId, _) = await _store.CreateAsync(document);

        // Act
        var retrieved = await _store.GetAsync(docId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(docId, retrieved.Id);
        Assert.Equal(document.Title, retrieved.Title);
        Assert.Equal(document.Goals, retrieved.Goals);
        Assert.Equal(document.TenantId, retrieved.TenantId);
        Assert.Equal(document.GymnastId, retrieved.GymnastId);
        Assert.Equal(document.CoachId, retrieved.CoachId);
    }

    [Fact]
    public async Task GetAsync_NonExistentDocument_ReturnsNull()
    {
        // Arrange
        var nonExistentId = "programme-nonexistent";

        // Act
        var result = await _store.GetAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ExistingDocument_ReturnsNewRev()
    {
        // Arrange
        var document = CreateTestDocument();
        var (docId, rev) = await _store.CreateAsync(document);

        document.Id = docId;
        document.Rev = rev;
        document.Title = "Updated Title";
        document.Goals = "Updated Goals";

        // Act
        var newRev = await _store.UpdateAsync(document);

        // Assert
        Assert.NotNull(newRev);
        Assert.NotEqual(rev, newRev);

        var updated = await _store.GetAsync(docId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Goals", updated.Goals);
        Assert.Equal(newRev, updated.Rev);
    }

    [Fact]
    public async Task UpdateAsync_WithoutId_ThrowsArgumentException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Id = string.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpdateAsync(document));
    }

    [Fact]
    public async Task UpdateAsync_WithoutRev_ThrowsArgumentException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Id = "programme-test";
        document.Rev = string.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpdateAsync(document));
    }

    [Fact]
    public async Task DeleteAsync_ExistingDocument_ReturnsTrue()
    {
        // Arrange
        var document = CreateTestDocument();
        var (docId, rev) = await _store.CreateAsync(document);

        // Act
        var result = await _store.DeleteAsync(docId, rev);

        // Assert
        Assert.True(result);

        var deleted = await _store.GetAsync(docId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentDocument_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = "programme-nonexistent";
        var fakeRev = "1-abc123";

        // Act
        var result = await _store.DeleteAsync(nonExistentId, fakeRev);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BulkGetAsync_MultipleDocuments_ReturnsAllDocuments()
    {
        // Arrange
        var doc1 = CreateTestDocument();
        doc1.Title = "Programme 1";
        var (docId1, _) = await _store.CreateAsync(doc1);

        var doc2 = CreateTestDocument();
        doc2.Title = "Programme 2";
        var (docId2, _) = await _store.CreateAsync(doc2);

        var doc3 = CreateTestDocument();
        doc3.Title = "Programme 3";
        var (docId3, _) = await _store.CreateAsync(doc3);

        // Act
        var results = await _store.BulkGetAsync(new[] { docId1, docId2, docId3 });

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, doc => Assert.NotNull(doc));
        Assert.Contains(results, doc => doc!.Title == "Programme 1");
        Assert.Contains(results, doc => doc!.Title == "Programme 2");
        Assert.Contains(results, doc => doc!.Title == "Programme 3");
    }

    [Fact]
    public async Task BulkGetAsync_MixedExistingAndNonExistent_ReturnsCorrectResults()
    {
        // Arrange
        var doc1 = CreateTestDocument();
        var (docId1, _) = await _store.CreateAsync(doc1);

        var nonExistentId = "programme-nonexistent";

        // Act
        var results = await _store.BulkGetAsync(new[] { docId1, nonExistentId });

        // Assert
        Assert.Equal(2, results.Count);
        Assert.NotNull(results[0]);
        Assert.Null(results[1]);
    }

    [Fact]
    public async Task BulkGetAsync_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var emptyList = Array.Empty<string>();

        // Act
        var results = await _store.BulkGetAsync(emptyList);

        // Assert
        Assert.Empty(results);
    }

    private static ProgrammeDocument CreateTestDocument()
    {
        return new ProgrammeDocument
        {
            TenantId = Guid.NewGuid(),
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

[CollectionDefinition("CouchDb")]
public class CouchDbCollection : ICollectionFixture<CouchDbFixture>
{
}
