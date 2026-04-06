using Common.Core;
using Microsoft.EntityFrameworkCore;
using Training.Application.Services;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;

namespace Training.Infrastructure.Services;

/// <summary>
/// Service for managing the global skills catalog with semantic search.
/// Skills are NOT tenant-scoped - they are shared across all tenants.
/// </summary>
public sealed class SkillService : ISkillService
{
    private readonly TrainingDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly SkillVectorSearchService _vectorSearchService;

    public SkillService(
        TrainingDbContext dbContext,
        IEmbeddingService embeddingService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorSearchService = new SkillVectorSearchService(dbContext);
    }

    public async Task<Result<Skill>> CreateAsync(
        string title,
        string description,
        int effectivenessRating,
        IReadOnlyList<GymnasticSection> sections,
        Guid tenantId,
        Guid userId,
        string? imageUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sections is null || sections.Count == 0)
                return Result.Failure<Skill>(ErrorType.Validation, "At least one gymnastics section is required");

            // Create skill entity
            var skill = Skill.Create(title, description, effectivenessRating, tenantId, userId, imageUrl);

            // Generate embedding from title and description
            var embeddingText = $"{title}\n{description}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, cancellationToken);
            skill.SetEmbedding(embedding);

            // Add skill to context
            _dbContext.Skills.Add(skill);

            // Create skill sections
            foreach (var section in sections)
            {
                var skillSection = SkillSection.Create(skill.Id, section);
                _dbContext.SkillSections.Add(skillSection);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Reload with sections
            var createdSkill = await _dbContext.Skills
                .Include(s => s.Sections)
                .FirstAsync(s => s.Id == skill.Id, cancellationToken);

            return Result.Success(createdSkill);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Skill>(ErrorType.Validation, ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<Skill>(ErrorType.Internal, $"Failed to create skill: {ex.Message}");
        }
    }

    public async Task<Result<Skill>> UpdateAsync(
        Guid id,
        string title,
        string description,
        int effectivenessRating,
        IReadOnlyList<GymnasticSection> sections,
        string? imageUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sections is null || sections.Count == 0)
                return Result.Failure<Skill>(ErrorType.Validation, "At least one gymnastics section is required");

            var skill = await _dbContext.Skills
                .Include(s => s.Sections)
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (skill is null)
                return Result.Failure<Skill>(ErrorType.NotFound, $"Skill with ID {id} not found");

            // Update skill properties
            skill.Update(title, description, effectivenessRating, imageUrl);

            // Regenerate embedding if title or description changed
            var embeddingText = $"{title}\n{description}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, cancellationToken);
            skill.SetEmbedding(embedding);

            // Replace sections: remove old, add new
            var existingSections = await _dbContext.SkillSections
                .Where(ss => ss.SkillId == id)
                .ToListAsync(cancellationToken);

            _dbContext.SkillSections.RemoveRange(existingSections);

            foreach (var section in sections)
            {
                var skillSection = SkillSection.Create(skill.Id, section);
                _dbContext.SkillSections.Add(skillSection);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Reload with sections
            var updatedSkill = await _dbContext.Skills
                .Include(s => s.Sections)
                .FirstAsync(s => s.Id == id, cancellationToken);

            return Result.Success(updatedSkill);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Skill>(ErrorType.Validation, ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<Skill>(ErrorType.Internal, $"Failed to update skill: {ex.Message}");
        }
    }

    public async Task<Result<Skill>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await _dbContext.Skills
            .Include(s => s.Sections)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (skill is null)
            return Result.Failure<Skill>(ErrorType.NotFound, $"Skill with ID {id} not found");

        return Result.Success(skill);
    }

    public async Task<Result<SkillListResult>> ListAsync(
        GymnasticSection? section = null,
        int? minRating = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageNumber < 1)
                return Result.Failure<SkillListResult>(ErrorType.Validation, "Page number must be at least 1");

            if (pageSize < 1 || pageSize > 100)
                return Result.Failure<SkillListResult>(ErrorType.Validation, "Page size must be between 1 and 100");

            var query = _dbContext.Skills
                .Include(s => s.Sections)
                .AsQueryable();

            // Apply section filter
            if (section.HasValue)
            {
                query = query.Where(s => s.Sections.Any(ss => ss.Section == section.Value));
            }

            // Apply rating filter
            if (minRating.HasValue)
            {
                query = query.Where(s => s.EffectivenessRating >= minRating.Value);
            }

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply ordering and pagination
            var skills = await query
                .OrderByDescending(s => s.UsageCount)
                .ThenByDescending(s => s.EffectivenessRating)
                .ThenBy(s => s.Title)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var result = new SkillListResult(skills, totalCount, pageNumber, pageSize);
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<SkillListResult>(ErrorType.Internal, $"Failed to list skills: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<SkillSearchResult>>> SearchAsync(
        string query,
        int maxResults = 10,
        GymnasticSection? section = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return Result.Failure<IReadOnlyList<SkillSearchResult>>(ErrorType.Validation, "Search query is required");

            if (maxResults < 1 || maxResults > 50)
                return Result.Failure<IReadOnlyList<SkillSearchResult>>(ErrorType.Validation, "Max results must be between 1 and 50");

            // Generate embedding for search query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            // Perform vector search
            var results = await _vectorSearchService.SearchSimilarSkillsAsync(
                queryEmbedding,
                maxResults,
                section,
                cancellationToken);

            return Result.Success(results);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<SkillSearchResult>>(ErrorType.Internal, $"Failed to search skills: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await _dbContext.Skills.FindAsync([id], cancellationToken);

        if (skill is null)
            return Result.Failure(ErrorType.NotFound, $"Skill with ID {id} not found");

        if (skill.UsageCount > 0)
            return Result.Failure(ErrorType.Conflict, $"Cannot delete skill '{skill.Title}' because it is in use in {skill.UsageCount} programme(s)");

        _dbContext.Skills.Remove(skill);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await _dbContext.Skills.FindAsync([id], cancellationToken);

        if (skill is null)
            return Result.Failure(ErrorType.NotFound, $"Skill with ID {id} not found");

        skill.IncrementUsageCount();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DecrementUsageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await _dbContext.Skills.FindAsync([id], cancellationToken);

        if (skill is null)
            return Result.Failure(ErrorType.NotFound, $"Skill with ID {id} not found");

        skill.DecrementUsageCount();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
