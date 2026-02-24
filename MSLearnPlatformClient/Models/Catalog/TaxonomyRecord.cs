using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class TaxonomyRecord
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("children")]
    public List<TaxonomyRecord>? Children { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
