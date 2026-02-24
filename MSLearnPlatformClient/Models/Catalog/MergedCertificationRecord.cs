using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class MergedCertificationRecord
{
    [JsonPropertyName("id")]
    public string? Uid { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("certificationType")]
    public TaxonomyRecord? CertificationType { get; set; }

    [JsonPropertyName("products")]
    public List<TaxonomyRecord>? Products { get; set; }

    [JsonPropertyName("levels")]
    public List<TaxonomyRecord>? Levels { get; set; }

    [JsonPropertyName("roles")]
    public List<TaxonomyRecord>? Roles { get; set; }

    [JsonPropertyName("subjects")]
    public List<TaxonomyRecord>? Subjects { get; set; }

    [JsonPropertyName("renewalFrequencyInDays")]
    public int? RenewalFrequencyInDays { get; set; }

    [JsonPropertyName("prerequisites")]
    public string[]? Prerequisites { get; set; }

    [JsonPropertyName("skills")]
    public string[]? Skills { get; set; }

    [JsonPropertyName("recommendationList")]
    public string[]? RecommendationList { get; set; }

    [JsonPropertyName("studyGuide")]
    public List<StudyGuideItem>? StudyGuide { get; set; }

    [JsonPropertyName("examDurationInMinutes")]
    public int? ExamDurationInMinutes { get; set; }

    [JsonPropertyName("locales")]
    public List<TaxonomyRecord>? Locales { get; set; }

    [JsonPropertyName("providers")]
    public List<Provider>? Providers { get; set; }

    [JsonPropertyName("careerPaths")]
    public List<CareerPath>? CareerPaths { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
