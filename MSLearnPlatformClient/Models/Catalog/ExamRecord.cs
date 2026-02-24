using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class ExamRecord
{
    [JsonPropertyName("id")]
    public string? Uid { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("pdfUrl")]
    public string? PdfDownloadUrl { get; set; }

    [JsonPropertyName("practiceTestUrl")]
    public string? PracticeTestUrl { get; set; }

    [JsonPropertyName("practiceAssessmentUrl")]
    public string? PracticeAssessmentUrl { get; set; }

    [JsonPropertyName("locales")]
    public List<TaxonomyRecord>? Locales { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("courses")]
    public List<IdReference>? Courses { get; set; }

    [JsonPropertyName("levels")]
    public List<TaxonomyRecord>? Levels { get; set; }

    [JsonPropertyName("roles")]
    public List<TaxonomyRecord>? Roles { get; set; }

    [JsonPropertyName("products")]
    public List<TaxonomyRecord>? Products { get; set; }

    [JsonPropertyName("providers")]
    public List<Provider>? Providers { get; set; }

    [JsonPropertyName("studyGuide")]
    public List<StudyGuideItem>? StudyGuide { get; set; }

    [JsonPropertyName("examNumber")]
    public string? ExamNumber { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
