using System.Net;
using System.Net.Http.Json;
using Auth.Infrastructure.Persistence;
using FluentAssertions;
using GymnasticsPlatform.Api.Endpoints;
using GymnasticsPlatform.Api.Models;
using GymnasticsPlatform.Api.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;
using Auth.Domain.Entities;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class SkillEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private async Task<HttpClient> CreateCoachClientAsync()
    {
        var client = factory.CreateClient();
        var email = $"coach-{Guid.NewGuid()}@example.com";
        var fullName = "Coach User";

        // Register and verify coach user
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Coach123!",
            FullName: fullName);
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        factory.MockKeycloakService.VerifyEmail(email);

        // Get user ID and tenant from database
        await Task.Delay(50);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userProfile = await db.UserProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (userProfile == null)
        {
            throw new InvalidOperationException($"User profile not found for email: {email}");
        }

        // Assign Coach role to user in database
        var userRole = UserRole.Create(
            userProfile.TenantId,
            userProfile.KeycloakUserId,
            Role.Coach,
            assignedBy: "system-test",
            TimeProvider.System);

        db.UserRoles.Add(userRole);
        await db.SaveChangesAsync();

        // Add coach authentication headers
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userProfile.KeycloakUserId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", userProfile.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        client.DefaultRequestHeaders.Add("X-Test-Username", fullName);

        return client;
    }

    [Fact]
    public async Task CreateSkill_WithValidRequest_Returns201Created()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var uniqueTitle = $"Handstand-{Guid.NewGuid().ToString()[..8]}";
        var request = new CreateSkillRequest(
            Title: uniqueTitle,
            Description: "Hold handstand position for 30 seconds with proper form",
            EffectivenessRating: 4,
            Sections: new[] { GymnasticSection.Floor, GymnasticSection.Bars }.ToList(),
            ImageUrl: "https://example.com/handstand.jpg");

        // Act
        var response = await client.PostAsJsonAsync("/api/skills", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var skill = await response.Content.ReadFromJsonAsync<SkillResponse>();
        skill.Should().NotBeNull();
        skill!.Title.Should().Be(uniqueTitle);
        skill.EffectivenessRating.Should().Be(4);
        skill.Sections.Should().Contain(GymnasticSection.Floor);
        skill.Sections.Should().Contain(GymnasticSection.Bars);
    }

    [Fact]
    public async Task CreateSkill_Unauthorized_Returns401()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new CreateSkillRequest(
            Title: "Test Skill",
            Description: "Test description",
            EffectivenessRating: 3,
            Sections: new[] { GymnasticSection.Floor }.ToList(),
            ImageUrl: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/skills", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSkill_ExistingId_Returns200WithSkill()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var uniqueTitle = $"Back Flip-{Guid.NewGuid().ToString()[..8]}";

        // Create a skill first
        var createRequest = new CreateSkillRequest(
            Title: uniqueTitle,
            Description: "Complete back flip with landing",
            EffectivenessRating: 5,
            Sections: new[] { GymnasticSection.Floor }.ToList(),
            ImageUrl: null);
        var createResponse = await client.PostAsJsonAsync("/api/skills", createRequest);
        var createdSkill = await createResponse.Content.ReadFromJsonAsync<SkillResponse>();

        // Act
        var response = await client.GetAsync($"/api/skills/{createdSkill!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var skill = await response.Content.ReadFromJsonAsync<SkillResponse>();
        skill.Should().NotBeNull();
        skill!.Id.Should().Be(createdSkill.Id);
        skill.Title.Should().Be(uniqueTitle);
    }

    [Fact]
    public async Task GetSkill_NonExistentId_Returns404()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/skills/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchSkills_WithQuery_ReturnsRankedResults()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var uniqueSuffix = Guid.NewGuid().ToString()[..8];

        // Create test skills
        await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest(
            $"Handstand Push-up-{uniqueSuffix}",
            "Perform push-up in handstand position",
            4,
            new[] { GymnasticSection.Floor }.ToList(),
            null));

        await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest(
            $"Beam Walk-{uniqueSuffix}",
            "Walk across balance beam",
            3,
            new[] { GymnasticSection.Beam }.ToList(),
            null));

        var searchRequest = new SearchSkillsRequest(
            Query: "handstand",
            MaxResults: 10,
            Section: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/skills/search", searchRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<SkillSearchResultResponse>>();
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();
        // Cosine similarity ranges from -1 (opposite) to 1 (identical)
        results!.All(r => r.SimilarityScore is >= -1 and <= 1).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSkill_WhenInUse_Returns409Conflict()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var uniqueTitle = $"Test Skill-{Guid.NewGuid().ToString()[..8]}";

        // Create a skill
        var createRequest = new CreateSkillRequest(
            uniqueTitle,
            "Test description",
            3,
            new[] { GymnasticSection.Floor }.ToList(),
            null);
        var createResponse = await client.PostAsJsonAsync("/api/skills", createRequest);
        var createdSkill = await createResponse.Content.ReadFromJsonAsync<SkillResponse>();

        // Manually increment usage count in database
        using var scope = factory.Services.CreateScope();
        var trainingDb = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var skill = await trainingDb.Skills.FindAsync(createdSkill!.Id);
        skill!.IncrementUsageCount();
        await trainingDb.SaveChangesAsync();

        // Act
        var response = await client.DeleteAsync($"/api/skills/{createdSkill.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteSkill_WhenNotInUse_Returns204NoContent()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var uniqueTitle = $"Deletable Skill-{Guid.NewGuid().ToString()[..8]}";

        // Create a skill
        var createRequest = new CreateSkillRequest(
            uniqueTitle,
            "Can be deleted",
            3,
            new[] { GymnasticSection.Floor }.ToList(),
            null);
        var createResponse = await client.PostAsJsonAsync("/api/skills", createRequest);
        var createdSkill = await createResponse.Content.ReadFromJsonAsync<SkillResponse>();

        // Act
        var response = await client.DeleteAsync($"/api/skills/{createdSkill!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await client.GetAsync($"/api/skills/{createdSkill.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListSkills_ReturnsSkills()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var uniqueSuffix = Guid.NewGuid().ToString()[..8];

        // Create test skills
        await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest(
            $"Skill 1-{uniqueSuffix}",
            "Description 1",
            4,
            new[] { GymnasticSection.Floor }.ToList(),
            null));

        await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest(
            $"Skill 2-{uniqueSuffix}",
            "Description 2",
            3,
            new[] { GymnasticSection.Bars }.ToList(),
            null));

        // Act
        var response = await client.GetAsync("/api/skills?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SkillListResponse>();
        result.Should().NotBeNull();
        result!.Skills.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);
    }
}

// Response DTOs for tests
public sealed record SkillResponse(
    Guid Id,
    string Title,
    string Description,
    int EffectivenessRating,
    string? ImageUrl,
    int UsageCount,
    IReadOnlyList<GymnasticSection> Sections,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt);

public sealed record SkillListResponse(
    IReadOnlyList<SkillResponse> Skills,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public sealed record SkillSearchResultResponse(
    SkillResponse Skill,
    double SimilarityScore);
