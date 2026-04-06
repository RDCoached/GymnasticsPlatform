using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Training.Application.Services;
using Training.Domain.Documents;
using Training.Infrastructure.Configuration;

namespace Training.Infrastructure.DocumentStore;

public sealed class CouchDbProgrammeDocumentStore : IProgrammeDocumentStore
{
    private readonly HttpClient _httpClient;
    private readonly string _databaseUrl;
    private readonly string _authHeader;
    private readonly JsonSerializerOptions _jsonOptions;

    public CouchDbProgrammeDocumentStore(HttpClient httpClient, IOptions<CouchDbSettings> settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        var config = settings.Value;

        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}"));
        _authHeader = $"Basic {credentials}";

        _databaseUrl = $"{config.ServerUrl.TrimEnd('/')}/{config.DatabaseName}";

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Don't use camelCase - let JsonPropertyName attributes control naming
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<(string DocId, string Rev)> CreateAsync(
        ProgrammeDocument document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(document.Id))
        {
            document.Id = $"programme-{Guid.NewGuid()}";
        }

        document.Type = "programme";
        document.CreatedAt = DateTimeOffset.UtcNow;
        document.LastModifiedAt = DateTimeOffset.UtcNow;

        // Serialize to JSON and remove _rev field for new documents
        var jsonString = JsonSerializer.Serialize(document, _jsonOptions);
        var jsonDoc = JsonDocument.Parse(jsonString);
        var root = jsonDoc.RootElement;

        var modifiedJson = new Dictionary<string, JsonElement>();
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name != "_rev" || !string.IsNullOrEmpty(property.Value.GetString()))
            {
                modifiedJson[property.Name] = property.Value.Clone();
            }
        }

        var content = new StringContent(
            JsonSerializer.Serialize(modifiedJson, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _databaseUrl) { Content = content };
        request.Headers.TryAddWithoutValidation("Authorization", _authHeader);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"CouchDB request failed with status {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CouchDbResponse>(_jsonOptions, cancellationToken);

        document.Id = result!.Id;
        document.Rev = result.Rev;

        return (result.Id, result.Rev);
    }

    public async Task<ProgrammeDocument?> GetAsync(
        string docId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_databaseUrl}/{docId}");
        request.Headers.TryAddWithoutValidation("Authorization", _authHeader);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<ProgrammeDocument>(
            _jsonOptions,
            cancellationToken);

        return document;
    }

    public async Task<string> UpdateAsync(
        ProgrammeDocument document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(document.Id))
            throw new ArgumentException("Document ID is required for update", nameof(document));

        if (string.IsNullOrEmpty(document.Rev))
            throw new ArgumentException("Document revision is required for update", nameof(document));

        document.LastModifiedAt = DateTimeOffset.UtcNow;

        var content = JsonContent.Create(document, options: _jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{_databaseUrl}/{document.Id}") { Content = content };
        request.Headers.TryAddWithoutValidation("Authorization", _authHeader);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CouchDbResponse>(_jsonOptions, cancellationToken);

        document.Rev = result!.Rev;

        return result.Rev;
    }

    public async Task<bool> DeleteAsync(
        string docId,
        string rev,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(docId))
            throw new ArgumentException("Document ID is required", nameof(docId));

        if (string.IsNullOrEmpty(rev))
            throw new ArgumentException("Revision is required for deletion", nameof(rev));

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{_databaseUrl}/{docId}?rev={rev}");
        request.Headers.TryAddWithoutValidation("Authorization", _authHeader);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<ProgrammeDocument?>> BulkGetAsync(
        IEnumerable<string> docIds,
        CancellationToken cancellationToken = default)
    {
        var ids = docIds.ToList();
        if (!ids.Any())
            return Array.Empty<ProgrammeDocument?>();

        var results = new List<ProgrammeDocument?>();

        foreach (var docId in ids)
        {
            var document = await GetAsync(docId, cancellationToken);
            results.Add(document);
        }

        return results;
    }

    private sealed record CouchDbResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("rev")] string Rev,
        [property: JsonPropertyName("ok")] bool Ok);
}
