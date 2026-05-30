using System.Text.Json.Serialization;

namespace Remarkable.Api.Client;

/// <summary>
/// Result of a successful PDF upload. The server assigns both the document id and the
/// content-addressed hash of the document collection.
/// </summary>
public sealed record UploadResult(
    [property: JsonPropertyName("docID")] string DocumentId,
    [property: JsonPropertyName("hash")] string Hash);
