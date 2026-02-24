using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class CourseRecord
{
    [JsonPropertyName("id")]
    public string? Uid { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("courseNumber")]
    public string? CourseNumber { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("durationInHours")]
    public int? DurationInHours { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("locales")]
    public List<TaxonomyRecord>? Locales { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("certification")]
    public IdReference? Certification { get; set; }

    [JsonPropertyName("exam")]
    public IdReference? Exam { get; set; }

    [JsonPropertyName("levels")]
    public List<TaxonomyRecord>? Levels { get; set; }

    [JsonPropertyName("roles")]
    public List<TaxonomyRecord>? Roles { get; set; }

    [JsonPropertyName("products")]
    public List<TaxonomyRecord>? Products { get; set; }

    [JsonPropertyName("studyGuide")]
    public List<StudyGuideItem>? StudyGuide { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
