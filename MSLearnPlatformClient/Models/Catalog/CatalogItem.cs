using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class CatalogItem
{
    [JsonPropertyName("id")]
    public string? Uid { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
