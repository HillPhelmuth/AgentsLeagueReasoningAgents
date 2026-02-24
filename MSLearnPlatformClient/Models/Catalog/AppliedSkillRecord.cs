using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class AppliedSkillRecord
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public string? Uid { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("levels")]
    public List<TaxonomyRecord>? Levels { get; set; }

    [JsonPropertyName("roles")]
    public List<TaxonomyRecord>? Roles { get; set; }

    [JsonPropertyName("products")]
    public List<TaxonomyRecord>? Products { get; set; }

    [JsonPropertyName("subjects")]
    public List<TaxonomyRecord>? Subjects { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

