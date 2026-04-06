using Training.Domain.Entities;
using Training.Domain.Enums;

namespace Training.Domain.Tests.Entities;

public sealed class ProgrammeBuilderSessionTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInstance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var gymnastId = Guid.NewGuid();
        var initialGoals = "Improve vault technique and landing stability";
        var ragScopeConfig = "{\"scopeType\":\"gymnast\",\"minSimilarityScore\":0.7}";

        // Act
        var session = ProgrammeBuilderSession.Create(
            tenantId,
            coachId,
            gymnastId,
            initialGoals,
            ragScopeConfig);

        // Assert
        Assert.NotNull(session);
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(tenantId, session.TenantId);
        Assert.Equal(coachId, session.CoachId);
        Assert.Equal(gymnastId, session.GymnastId);
        Assert.Equal(initialGoals, session.InitialGoals);
        Assert.Equal(ragScopeConfig, session.RagScopeConfig);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Null(session.ResultingProgrammeId);
        Assert.Null(session.CompletedAt);
        Assert.True(session.StartedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Create_WithEmptyGoals_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var gymnastId = Guid.NewGuid();
        var emptyGoals = "";
        var ragScopeConfig = "{\"scopeType\":\"gymnast\"}";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ProgrammeBuilderSession.Create(
                tenantId,
                coachId,
                gymnastId,
                emptyGoals,
                ragScopeConfig));
    }

    [Fact]
    public void AppendConversation_AddsToConversationHistory()
    {
        // Arrange
        var session = CreateValidSession();
        var userMessage = "Can we add more plyometrics?";
        var assistantResponse = "Yes, I'll add box jumps and depth jumps to week 3.";

        // Act
        session.AppendConversation(userMessage, assistantResponse);

        // Assert
        Assert.NotNull(session.ConversationHistoryJson);
        var history = System.Text.Json.JsonSerializer.Deserialize<List<ConversationTurn>>(session.ConversationHistoryJson);
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal(userMessage, history[0].UserMessage);
        Assert.Equal(assistantResponse, history[0].AssistantResponse);
    }

    [Fact]
    public void Complete_WithProgrammeId_SetsStatusAndCompletedAt()
    {
        // Arrange
        var session = CreateValidSession();
        var programmeId = Guid.NewGuid();
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Null(session.ResultingProgrammeId);

        // Act
        session.Complete(programmeId);

        // Assert
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal(programmeId, session.ResultingProgrammeId);
        Assert.NotNull(session.CompletedAt);
        Assert.True(session.CompletedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Abandon_SetsStatusToAbandoned()
    {
        // Arrange
        var session = CreateValidSession();
        Assert.Equal(SessionStatus.Active, session.Status);

        // Act
        session.Abandon();

        // Assert
        Assert.Equal(SessionStatus.Abandoned, session.Status);
        Assert.NotNull(session.CompletedAt);
        Assert.True(session.CompletedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateValidSession();
        var programmeId = Guid.NewGuid();
        session.Complete(programmeId);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            session.Complete(Guid.NewGuid()));
    }

    [Fact]
    public void Abandon_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateValidSession();
        var programmeId = Guid.NewGuid();
        session.Complete(programmeId);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => session.Abandon());
    }

    private static ProgrammeBuilderSession CreateValidSession()
    {
        return ProgrammeBuilderSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Improve vault technique",
            "{\"scopeType\":\"gymnast\",\"minSimilarityScore\":0.7}");
    }
}
