using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public sealed class CatalogResponse
{
    [JsonPropertyName("modules")]
    public List<ModuleRecord> Modules { get; set; } = [];

    [JsonPropertyName("units")]
    public List<UnitRecord> Units { get; set; } = [];

    [JsonPropertyName("learningPaths")]
    public List<LearningPathRecord> LearningPaths { get; set; } = [];

    [JsonPropertyName("appliedSkills")]
    public List<AppliedSkillRecord> AppliedSkills { get; set; } = [];

    [JsonPropertyName("mergedCertifications")]
    public List<MergedCertificationRecord> MergedCertifications { get; set; } = [];

    [JsonPropertyName("certifications")]
    public List<CertificationRecord> Certifications { get; set; } = [];

    [JsonPropertyName("exams")]
    public List<ExamRecord> Exams { get; set; } = [];

    [JsonPropertyName("courses")]
    public List<CourseRecord> Courses { get; set; } = [];

    [JsonPropertyName("levels")]
    public List<TaxonomyRecord> Levels { get; set; } = [];

    [JsonPropertyName("products")]
    public List<TaxonomyRecord> Products { get; set; } = [];

    [JsonPropertyName("roles")]
    public List<TaxonomyRecord> Roles { get; set; } = [];

    [JsonPropertyName("subjects")]
    public List<TaxonomyRecord> Subjects { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
public class ModuleResponse
{
    [Description("Reasons for your selections")]
    public required string Reasoning { get; set; }
    [JsonPropertyName("modules")]
    [Description("The most relevent modules to the query")]
    
    public List<ModuleRecord> Modules { get; set; } = [];
    public List<ModuleResponseItem> ModuleItems { get; set; } = [];
}
public class ModuleFilterResponse
{
    [Description("Reasons for your selections")]
    public required string Reasoning { get; set; }
    [JsonPropertyName("modules")]
    [Description("The most relevent modules to the query")]
    public List<ModuleResponseItem> Modules { get; set; } = [];
}

public class ModuleResponseItem(string id, string title)
{
    public string Id { get; set; } = id;
    public string Title { get; set; } = title;
}
public class CoursesAndLearnPathResponse
{
    [Description("Reasons for your selections")]
    public required string Reasoning { get; set; }
    [JsonPropertyName("learningPaths")]
    [Description("The most relevent learning paths to the query")]
    public List<LearningPathRecord> LearningPaths { get; set; } = [];
    //[JsonPropertyName("courses")]
    //[Description("The most relevent courses to the query")]
    //public List<AppliedSkillRecord> AppliedSkills { get; set; } = [];
}
public class ExamAndCertResponse
{
    [Description("Reasons for your selections")]
    public required string Reasoning { get; set; }
    [JsonPropertyName("exams")]
    [Description("The exams that may be applicable to the query")]
    public List<ExamRecord> Exams { get; set; } = [];
    [JsonPropertyName("certifications")]
    [Description("The certifications that may be applicable to the query")]
    public List<CertificationRecord> Certifications { get; set; } = [];
    [JsonPropertyName("appliedSkills")]
    [Description("The applied skills that may be applicable to the query")]
    public List<AppliedSkillRecord> AppliedSkills { get; set; } = [];
}
