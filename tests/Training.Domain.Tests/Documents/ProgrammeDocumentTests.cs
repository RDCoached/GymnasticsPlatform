using System.Text.Json;
using Training.Domain.Documents;

namespace Training.Domain.Tests.Documents;

public sealed class ProgrammeDocumentTests
{
    [Fact]
    public void SerializeAndDeserialize_PreservesAllProperties()
    {
        // Arrange
        var document = new ProgrammeDocument
        {
            Id = "programme-abc123",
            Rev = "1-xyz789",
            Type = "programme",
            TenantId = Guid.NewGuid(),
            GymnastId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Summer Vault Program",
            Goals = "Improve landing stability, build upper body strength",
            Content = new ProgrammeContent
            {
                Weeks =
                [
                    new WeekContent
                    {
                        WeekNumber = 1,
                        Focus = "Vault technique fundamentals",
                        Exercises =
                        [
                            new ExerciseContent
                            {
                                Name = "Handstand holds",
                                Sets = 3,
                                Reps = "30s",
                                Notes = "Focus on straight body line, engage core"
                            },
                            new ExerciseContent
                            {
                                Name = "Box jumps",
                                Sets = 4,
                                Reps = "10",
                                Notes = "Explosive power, soft landing"
                            }
                        ],
                        Notes = "Monitor shoulder fatigue, adjust if needed"
                    }
                ],
                Progressions = ["Week 1: 20s hold", "Week 4: 45s hold", "Week 8: 60s hold"],
                GeneralNotes = "Adjust intensity based on recovery. Focus on form over speed."
            },
            StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var deserialized = JsonSerializer.Deserialize<ProgrammeDocument>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(document.Id, deserialized.Id);
        Assert.Equal(document.Rev, deserialized.Rev);
        Assert.Equal(document.Type, deserialized.Type);
        Assert.Equal(document.TenantId, deserialized.TenantId);
        Assert.Equal(document.GymnastId, deserialized.GymnastId);
        Assert.Equal(document.CoachId, deserialized.CoachId);
        Assert.Equal(document.Title, deserialized.Title);
        Assert.Equal(document.Goals, deserialized.Goals);
        Assert.NotNull(deserialized.Content);
        Assert.Single(deserialized.Content.Weeks);
        Assert.Equal(2, deserialized.Content.Weeks[0].Exercises.Count);
        Assert.Equal("Handstand holds", deserialized.Content.Weeks[0].Exercises[0].Name);
    }

    [Fact]
    public void Create_WithMinimalData_ReturnsValidDocument()
    {
        // Arrange & Act
        var document = new ProgrammeDocument
        {
            Id = "programme-test",
            Rev = "1-abc",
            Type = "programme",
            TenantId = Guid.NewGuid(),
            GymnastId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Test Programme",
            Goals = "Test goals",
            Content = new ProgrammeContent
            {
                Weeks = [],
                Progressions = [],
                GeneralNotes = ""
            },
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.NotNull(document);
        Assert.Equal("programme", document.Type);
        Assert.NotNull(document.Content);
        Assert.Empty(document.Content.Weeks);
    }

    [Fact]
    public void SerializeToJson_ProducesCamelCaseProperties()
    {
        // Arrange
        var document = new ProgrammeDocument
        {
            Id = "programme-test",
            Rev = "1-abc",
            Type = "programme",
            TenantId = Guid.NewGuid(),
            GymnastId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Test",
            Goals = "Goals",
            Content = new ProgrammeContent { Weeks = [], Progressions = [], GeneralNotes = "" },
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        Assert.Contains("\"_id\":", json); // CouchDB convention
        Assert.Contains("\"_rev\":", json);
        Assert.Contains("\"type\":", json);
        Assert.Contains("\"tenantId\":", json);
        Assert.Contains("\"gymnastId\":", json);
        Assert.Contains("\"title\":", json);
    }
}
