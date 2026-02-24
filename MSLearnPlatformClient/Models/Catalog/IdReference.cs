using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class IdReference
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}