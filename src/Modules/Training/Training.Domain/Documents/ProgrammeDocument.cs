using System.Text.Json.Serialization;

namespace Training.Domain.Documents;

/// <summary>
/// CouchDB document model for programme content.
/// Stored in CouchDB with flexible schema and automatic versioning.
/// </summary>
public sealed class ProgrammeDocument
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("_rev")]
    public string Rev { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "programme";

    [JsonPropertyName("tenantId")]
    public Guid TenantId { get; set; }

    [JsonPropertyName("gymnastId")]
    public Guid GymnastId { get; set; }

    [JsonPropertyName("coachId")]
    public Guid CoachId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("goals")]
    public string Goals { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public ProgrammeContent Content { get; set; } = new();

    [JsonPropertyName("startDate")]
    public DateTimeOffset StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTimeOffset EndDate { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("lastModifiedAt")]
    public DateTimeOffset LastModifiedAt { get; set; }
}

public sealed class ProgrammeContent
{
    [JsonPropertyName("weeks")]
    public List<WeekContent> Weeks { get; set; } = [];

    [JsonPropertyName("progressions")]
    public List<string> Progressions { get; set; } = [];

    [JsonPropertyName("generalNotes")]
    public string GeneralNotes { get; set; } = string.Empty;
}

public sealed class WeekContent
{
    [JsonPropertyName("weekNumber")]
    public int WeekNumber { get; set; }

    [JsonPropertyName("focus")]
    public string Focus { get; set; } = string.Empty;

    [JsonPropertyName("exercises")]
    public List<ExerciseContent> Exercises { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public sealed class ExerciseContent
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("skillId")]
    public Guid? SkillId { get; set; }

    [JsonPropertyName("sets")]
    public int Sets { get; set; }

    [JsonPropertyName("reps")]
    public string Reps { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
