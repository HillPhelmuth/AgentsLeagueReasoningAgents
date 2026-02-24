using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace MSLearnPlatformClient.Models.Catalog;

public enum CatalogItemType
{
    [Description("modules")]
    Module,
    [Description("units")]
    Unit,
    [Description("learning-paths")]
    LearningPath,
    [Description("applied-skills")]
    AppliedSkill,
    [Description("certifications")]
    Certification,
    [Description("merged-certifications")]
    MergedCertification,
    [Description("exams")]
    Exam,
    [Description("courses")]
    Course,
    [Description("levels")]
    Level,
    [Description("roles")]
    Role,
    [Description("products")]
    Product,
    [Description("subjects")]
    Subject
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LevelEnum { Advanced, Beginner, Intermediate };

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LearnSubject
{
    [Description("application-development")]
    ApplicationDevelopment,
    [Description("backend-development")] BackendDevelopment,
    [Description("devops")] DevOps,
    [Description("artificial-intelligence")]
    ArtificialIntelligence,
    [Description("machine-learning")] MachineLearning,
    [Description("business-applications")] BusinessApplications,
    [Description("automation")] Automation,
    [Description("business-reporting")] BusinessReporting,
    [Description("custom-app-development")]
    CustomAppDevelopment,
    [Description("customer-relationship-management")]
    CustomerRelationshipManagement,
    [Description("finance-and-accounting")]
    FinanceAndAccounting,
    [Description("marketing-and-sales")] MarketingAndSales,
    [Description("solution-design")] SolutionDesign,
    [Description("supply-chain-management")]
    SupplyChainManagement,
    [Description("data-management")] DataManagement,
    [Description("data-analytics")] DataAnalytics,
    [Description("data-engineering")] DataEngineering,
    [Description("databases")] Databases,
    [Description("security")] Security,
    [Description("information-protection-and-governance")]
    InformationProtectionAndGovernance,
    [Description("technical-infrastructure")]
    TechnicalInfrastructure,
    [Description("application-management")]
    ApplicationManagement,
    [Description("migration")] Migration
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role
{
    [Description("administrator")] Administrator,
    [Description("ai-edge-engineer")] AiEdgeEngineer,
    [Description("ai-engineer")] AiEngineer,
    [Description("auditor")] Auditor,
    [Description("business-analyst")] BusinessAnalyst,
    [Description("business-leader")] BusinessLeader,
    [Description("business-owner")] BusinessOwner,
    [Description("business-user")] BusinessUser,
    [Description("data-analyst")] DataAnalyst,
    [Description("data-engineer")] DataEngineer,
    [Description("data-scientist")] DataScientist,
    [Description("database-administrator")] DatabaseAdministrator,
    [Description("developer")] Developer,
    [Description("devops-engineer")] DevopsEngineer,
    [Description("functional-consultant")] FunctionalConsultant,
    [Description("higher-ed-educator")] HigherEdEducator,
    [Description("identity-access-admin")] IdentityAccessAdmin,
    [Description("ip-admin")] IpAdmin,
    [Description("k-12-educator")] K12Educator,
    [Description("maker")] AppMakerIncludingAIApps,
    [Description("network-engineer")] NetworkEngineer,
    [Description("parent-guardian")] ParentGuardian,
    [Description("platform-engineer")] PlatformEngineer,
    [Description("privacy-manager")] PrivacyManager,
    [Description("risk-practitioner")] RiskPractitioner,
    [Description("school-leader")] SchoolLeader,
    [Description("security-engineer")] SecurityEngineer,
    [Description("security-operations-analyst")] SecurityOperationsAnalyst,
    [Description("service-adoption-specialist")] ServiceAdoptionSpecialist,
    [Description("solution-architect")] SolutionArchitect,
    [Description("startup-founder")] StartupFounder,
    [Description("support-engineer")] SupportEngineer,
    [Description("technical-writer")] TechnicalWriter,
    [Description("technology-manager")] TechnologyManager
}
public static class EnumExtensions
{
    public static string ToDescriptionString<T>(this T val) where T : Enum
    {
        var attributes = (DescriptionAttribute[])val
            .GetType()
            .GetField(val.ToString())!
            .GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : val.ToString();
    }
}

public sealed record CatalogQuery
{
    public string? Locale { get; init; }
    public CatalogItemType[]? Type { get; set; }
    public string[]? Uid { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    /// <summary>
    /// An operator and value to filter by the popularity value (in a range of 0-1) of objects. Operator includes lt (less than),
    /// lte (less than or equal to), eq (equal to), gt (greater than), gte (greater than or equal to).
    /// When you use this parameter, the operator will default to gte if not specified. Example: `?popularity=gte 0.5`
    /// </summary>
    //public Popularity[]? Popularity { get; init; }
    //private string[]? PopularityAsStrings => Popularity?.Select(x => x.ToString()).ToArray();
    public LevelEnum[]? Levels { get; init; }
    public string[]? LevelAsStrings => Levels?.Select(x => x.ToString().ToLower()).ToArray();
    public Role[]? Roles { get; init; }
    public string[]? RoleAsStrings => Roles?.Select(x => x.ToDescriptionString()).ToArray();
    public string[]? Products { get; init; }
    public LearnSubject[]? Subjects { get; init; }
    public string[]? SubjectsAsStrings { get; init; }
    public int MaxPageSize { get; init; } = 50;

    public IReadOnlyList<(string Key, string Value)> ToQueryParameters(string? defaultLocale = null)
    {
        var parameters = new List<(string Key, string Value)>();

        //var effectiveLocale = Locale ?? defaultLocale;
        //if (!string.IsNullOrWhiteSpace(effectiveLocale))
        //{
        //    parameters.Add(("locale", effectiveLocale!));
        //}

        if (Type is { Length: > 0 })
        {
            parameters.Add(("type", string.Join(',', Type.Select(GetTypeValue))));
        }

        //if (Uid is { Length: > 0 })
        //{
        //    parameters.Add(("uid", string.Join(',', Uid)));
        //}

        if (LastModified is not null)
        {
            parameters.Add(("updatedAt.gt", LastModified.Value.ToString("O", CultureInfo.InvariantCulture)));
        }

        AddArray(parameters, "levels", Levels?.Select(x => x.ToString().ToLower()).ToArray());
        AddArray(parameters, "roles", RoleAsStrings);
        AddArray(parameters, "products", Products);
        AddArray(parameters, "subjects", SubjectsAsStrings);
        parameters.Add(("maxpagesize", MaxPageSize.ToString()));
        parameters.Add(("api-version", "2023-11-01-preview"));

        return parameters;
    }

    public string ToQueryString(string? defaultLocale = null)
    {
        var parameters = ToQueryParameters(defaultLocale)
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .ThenBy(parameter => parameter.Value, StringComparer.Ordinal)
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value.ToLower())}");

        return string.Join('&', parameters);
    }

    private static void AddArray(List<(string Key, string Value)> parameters, string key, string[]? values)
    {
        if (values is { Length: > 0 })
        {
            parameters.Add((key, string.Join(',', values)));
        }
    }

    public static string GetTypeValue(CatalogItemType type) => type switch
    {
        CatalogItemType.Module => "modules",
        CatalogItemType.Unit => "units",
        CatalogItemType.LearningPath => "learningPaths",
        CatalogItemType.AppliedSkill => "appliedSkills",
        CatalogItemType.Certification => "certifications",
        CatalogItemType.MergedCertification => "mergedCertifications",
        CatalogItemType.Exam => "exams",
        CatalogItemType.Course => "courses",
        CatalogItemType.Level => "levels",
        CatalogItemType.Role => "roles",
        CatalogItemType.Product => "products",
        CatalogItemType.Subject => "subjects",
        _ => type.ToString().ToLowerInvariant()
    };
}
