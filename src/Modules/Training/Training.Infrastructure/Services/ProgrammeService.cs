using System.Text;
using Microsoft.EntityFrameworkCore;
using Training.Application.Services;
using Training.Domain.Documents;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;

namespace Training.Infrastructure.Services;

public sealed class ProgrammeService : IProgrammeService
{
    private readonly TrainingDbContext _dbContext;
    private readonly IProgrammeDocumentStore _documentStore;
    private readonly IEmbeddingService _embeddingService;

    public ProgrammeService(
        TrainingDbContext dbContext,
        IProgrammeDocumentStore documentStore,
        IEmbeddingService embeddingService)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _embeddingService = embeddingService;
    }

    public async Task<ProgrammeMetadata> CreateAsync(
        ProgrammeDocument document,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Write to CouchDB (source of truth for content)
        var (docId, rev) = await _documentStore.CreateAsync(document, cancellationToken);

        // Step 2: Generate embedding from programme content
        var contentForEmbedding = BuildEmbeddingText(document);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(contentForEmbedding, cancellationToken);

        // Step 3: Create PostgreSQL metadata with CouchDB reference
        var metadata = ProgrammeMetadata.Create(
            document.TenantId,
            document.GymnastId,
            document.CoachId,
            docId,
            rev,
            document.Title,
            document.StartDate,
            document.EndDate);

        metadata.SetEmbedding(embedding);

        _dbContext.ProgrammeMetadata.Add(metadata);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return metadata;
    }

    public async Task<(ProgrammeMetadata Metadata, ProgrammeDocument Document)?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Get metadata from PostgreSQL
        var metadata = await _dbContext.ProgrammeMetadata
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (metadata is null)
            return null;

        // Step 2: Fetch full document from CouchDB
        var document = await _documentStore.GetAsync(metadata.CouchDbDocId, cancellationToken);

        if (document is null)
            return null;

        return (metadata, document);
    }

    public async Task<ProgrammeMetadata> UpdateAsync(
        Guid id,
        ProgrammeDocument document,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Get existing metadata
        var metadata = await _dbContext.ProgrammeMetadata
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (metadata is null)
            throw new InvalidOperationException($"Programme with ID {id} not found");

        // Step 2: Update CouchDB document (gets new revision)
        var newRev = await _documentStore.UpdateAsync(document, cancellationToken);

        // Step 3: Generate new embedding
        var contentForEmbedding = BuildEmbeddingText(document);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(contentForEmbedding, cancellationToken);

        // Step 4: Update PostgreSQL metadata
        metadata.UpdateCouchDbRev(newRev);
        metadata.SetEmbedding(embedding);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return metadata;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Step 1: Get metadata
        var metadata = await _dbContext.ProgrammeMetadata
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (metadata is null)
            return false;

        // Step 2: Delete from CouchDB
        var couchDbDeleted = await _documentStore.DeleteAsync(
            metadata.CouchDbDocId,
            metadata.CouchDbRev,
            cancellationToken);

        // Step 3: Delete from PostgreSQL (even if CouchDB delete failed)
        _dbContext.ProgrammeMetadata.Remove(metadata);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return couchDbDeleted;
    }

    public async Task<ProgrammeMetadata> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Step 1: Get the programme to activate
        var metadata = await _dbContext.ProgrammeMetadata
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (metadata is null)
            throw new InvalidOperationException($"Programme with ID {id} not found");

        // Step 2: Deactivate any other active programmes for the same gymnast
        var activeProgrammes = await _dbContext.ProgrammeMetadata
            .Where(p => p.GymnastId == metadata.GymnastId && p.Status == ProgrammeStatus.Active && p.Id != id)
            .ToListAsync(cancellationToken);

        foreach (var activeProgramme in activeProgrammes)
        {
            activeProgramme.Complete();
        }

        // Save deactivations first to avoid unique constraint violation
        if (activeProgrammes.Any())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Step 3: Activate the target programme
        metadata.Activate();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return metadata;
    }

    public async Task<ProgrammeMetadata> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var metadata = await _dbContext.ProgrammeMetadata
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (metadata is null)
            throw new InvalidOperationException($"Programme with ID {id} not found");

        metadata.Complete();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return metadata;
    }

    public async Task<ProgrammeMetadata> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var metadata = await _dbContext.ProgrammeMetadata
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (metadata is null)
            throw new InvalidOperationException($"Programme with ID {id} not found");

        metadata.Archive();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return metadata;
    }

    public async Task<IReadOnlyList<ProgrammeMetadata>> ListByGymnastAsync(
        Guid gymnastId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProgrammeMetadata
            .Where(p => p.GymnastId == gymnastId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static string BuildEmbeddingText(ProgrammeDocument document)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Title: {document.Title}");
        sb.AppendLine($"Goals: {document.Goals}");

        if (document.Content?.Weeks is not null)
        {
            foreach (var week in document.Content.Weeks)
            {
                sb.AppendLine($"Week {week.WeekNumber} Focus: {week.Focus}");

                if (week.Exercises is not null)
                {
                    foreach (var exercise in week.Exercises)
                    {
                        sb.AppendLine($"Exercise: {exercise.Name} - {exercise.Sets} sets x {exercise.Reps}");
                        if (!string.IsNullOrWhiteSpace(exercise.Notes))
                        {
                            sb.AppendLine($"Notes: {exercise.Notes}");
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(week.Notes))
                {
                    sb.AppendLine($"Week Notes: {week.Notes}");
                }
            }
        }

        if (document.Content?.Progressions is not null)
        {
            sb.AppendLine("Progressions:");
            foreach (var progression in document.Content.Progressions)
            {
                sb.AppendLine(progression);
            }
        }

        if (!string.IsNullOrWhiteSpace(document.Content?.GeneralNotes))
        {
            sb.AppendLine($"General Notes: {document.Content.GeneralNotes}");
        }

        return sb.ToString();
    }
}
