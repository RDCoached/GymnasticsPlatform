using Training.Domain.Documents;
using Training.Domain.Entities;

namespace Training.Application.Services;

/// <summary>
/// Service for managing programmes with hybrid storage (PostgreSQL metadata + CouchDB documents).
/// Coordinates dual-write operations between PostgreSQL and CouchDB.
/// </summary>
public interface IProgrammeService
{
    /// <summary>
    /// Creates a new programme with dual-write to CouchDB and PostgreSQL.
    /// Flow: Write CouchDB → Generate embedding → Write PostgreSQL metadata
    /// </summary>
    /// <param name="document">Programme document content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Programme metadata with CouchDB references</returns>
    Task<ProgrammeMetadata> CreateAsync(ProgrammeDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a programme by ID, fetching metadata from PostgreSQL and full document from CouchDB.
    /// </summary>
    /// <param name="id">Programme metadata ID (not CouchDB doc ID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of metadata and document, or null if not found</returns>
    Task<(ProgrammeMetadata Metadata, ProgrammeDocument Document)?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a programme with dual-write to CouchDB and PostgreSQL.
    /// Flow: Update CouchDB (new rev) → Update PostgreSQL metadata
    /// </summary>
    /// <param name="id">Programme metadata ID</param>
    /// <param name="document">Updated programme document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated programme metadata</returns>
    Task<ProgrammeMetadata> UpdateAsync(Guid id, ProgrammeDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a programme from both CouchDB and PostgreSQL.
    /// </summary>
    /// <param name="id">Programme metadata ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a programme and deactivates any other active programmes for the same gymnast.
    /// Business rule: Only one active programme per gymnast.
    /// </summary>
    /// <param name="id">Programme metadata ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated programme metadata</returns>
    Task<ProgrammeMetadata> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a programme as completed.
    /// </summary>
    /// <param name="id">Programme metadata ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated programme metadata</returns>
    Task<ProgrammeMetadata> CompleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a completed programme.
    /// </summary>
    /// <param name="id">Programme metadata ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated programme metadata</returns>
    Task<ProgrammeMetadata> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all programmes for a specific gymnast.
    /// </summary>
    /// <param name="gymnastId">Gymnast ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of programme metadata (without full documents)</returns>
    Task<IReadOnlyList<ProgrammeMetadata>> ListByGymnastAsync(Guid gymnastId, CancellationToken cancellationToken = default);
}
