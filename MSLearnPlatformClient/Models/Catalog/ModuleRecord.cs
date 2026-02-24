using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class ModuleRecord
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("levels")]
    public List<TaxonomyRecord>? Levels { get; set; }

    [JsonPropertyName("roles")]
    public List<TaxonomyRecord>? Roles { get; set; }

    [JsonPropertyName("products")]
    public List<TaxonomyRecord>? Products { get; set; }

    [JsonPropertyName("subjects")]
    public List<TaxonomyRecord>? Subjects { get; set; }

    [JsonPropertyName("id")]
    public string? Uid { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("durationInMinutes")]
    public int? DurationInMinutes { get; set; }

    [JsonPropertyName("rating")]
    public Rating? Rating { get; set; }

    [JsonPropertyName("popularity")]
    public double? Popularity { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("socialImageUrl")]
    public string? SocialImageUrl { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("firstUnitUrl")]
    public string? FirstUnitUrl { get; set; }

    [JsonPropertyName("units")]
    public List<IdReference>? Units { get; set; }

    [JsonPropertyName("number_of_children")]
    public int? NumberOfChildren { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
