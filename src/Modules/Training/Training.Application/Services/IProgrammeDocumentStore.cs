using Training.Domain.Documents;

namespace Training.Application.Services;

/// <summary>
/// Abstraction for CouchDB document storage operations.
/// Manages ProgrammeDocument CRUD operations with CouchDB-specific features (revisions, bulk operations).
/// </summary>
public interface IProgrammeDocumentStore
{
    /// <summary>
    /// Creates a new programme document in CouchDB.
    /// </summary>
    /// <param name="document">The programme document to create (Id and Rev will be generated)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document ID and revision after creation</returns>
    Task<(string DocId, string Rev)> CreateAsync(ProgrammeDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a programme document by ID from CouchDB.
    /// </summary>
    /// <param name="docId">The CouchDB document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The programme document or null if not found</returns>
    Task<ProgrammeDocument?> GetAsync(string docId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing programme document in CouchDB.
    /// Requires the current revision to prevent conflicts.
    /// </summary>
    /// <param name="document">The programme document with updated content and current Rev</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New revision after update</returns>
    Task<string> UpdateAsync(ProgrammeDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a programme document from CouchDB.
    /// </summary>
    /// <param name="docId">The CouchDB document ID</param>
    /// <param name="rev">The current revision (required for deletion)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteAsync(string docId, string rev, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves multiple programme documents by their IDs in a single request.
    /// More efficient than multiple GetAsync calls.
    /// </summary>
    /// <param name="docIds">Collection of document IDs to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of programme documents (nulls for not found documents)</returns>
    Task<IReadOnlyList<ProgrammeDocument?>> BulkGetAsync(IEnumerable<string> docIds, CancellationToken cancellationToken = default);
}
