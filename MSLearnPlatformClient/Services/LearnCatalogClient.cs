using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSLearnPlatformClient.Abstractions;
using MSLearnPlatformClient.Models.Catalog;
using MSLearnPlatformClient.Options;

namespace MSLearnPlatformClient.Services;

public sealed class LearnCatalogClient(
    HttpClient httpClient,
    IOptions<LearnCatalogOptions> options,
    ILearnAccessTokenProvider accessTokenProvider,
    ILogger<LearnCatalogClient> logger)
    : ILearnCatalogClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LearnCatalogOptions _options = options.Value;


    public async Task<CatalogResponse> QueryCatalogAsync(CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = new CatalogResponse();
        var typeFilter = BuildTypeFilter(query.Type);

        if (Includes(typeFilter, CatalogItemType.Module))
        {
            result.Modules = await FetchPagedAsync<ModuleRecord>("modules", query, cancellationToken).ConfigureAwait(false);
        }

        if (Includes(typeFilter, CatalogItemType.LearningPath))
        {
            result.LearningPaths = await FetchPagedAsync<LearningPathRecord>("learning-paths", query, cancellationToken).ConfigureAwait(false);
        }

        if (Includes(typeFilter, CatalogItemType.Course))
        {
            result.Courses = await FetchPagedAsync<CourseRecord>("courses", query, cancellationToken).ConfigureAwait(false);
        }

        if (Includes(typeFilter, CatalogItemType.Certification) || Includes(typeFilter, CatalogItemType.MergedCertification))
        {
            var certifications = await FetchPagedAsync<CertificationRecord>("certifications", query, cancellationToken).ConfigureAwait(false);
            if (Includes(typeFilter, CatalogItemType.Certification))
            {
                result.Certifications = certifications;
            }

            if (Includes(typeFilter, CatalogItemType.MergedCertification))
            {
                result.MergedCertifications = certifications.Select(MapMergedCertification).ToList();
            }
        }

        if (Includes(typeFilter, CatalogItemType.AppliedSkill))
        {
            result.AppliedSkills = await FetchPagedAsync<AppliedSkillRecord>("applied-skills", query, cancellationToken).ConfigureAwait(false);
        }
        
        if (Includes(typeFilter, CatalogItemType.Exam))
        {
            var fetchExams = await FetchPagedAsync<ExamRecord>("exams", query, cancellationToken).ConfigureAwait(false);
            result.Exams = fetchExams;
        }

        if (Includes(typeFilter, CatalogItemType.Level))
        {
            result.Levels = await FetchPagedAsync<TaxonomyRecord>("levels", query, cancellationToken).ConfigureAwait(false);
        }

        if (Includes(typeFilter, CatalogItemType.Role))
        {
            result.Roles = await FetchPagedAsync<TaxonomyRecord>("roles", query, cancellationToken).ConfigureAwait(false);
        }

        if (Includes(typeFilter, CatalogItemType.Product))
        {
            result.Products = await FetchPagedAsync<TaxonomyRecord>("products", query, cancellationToken).ConfigureAwait(false);
        }

        if (Includes(typeFilter, CatalogItemType.Subject))
        {
            result.Subjects = await FetchPagedAsync<TaxonomyRecord>("subjects", query, cancellationToken).ConfigureAwait(false);
        }
        Console.WriteLine($"Retrieved {result.Exams.Count} exams, {result.Modules.Count} modules, {result.LearningPaths.Count} learning paths, {result.Courses.Count} courses, {result.Certifications.Count} certifications, {result.MergedCertifications.Count} merged certifications, {result.AppliedSkills.Count} applied skills");
        return result;
    }
    public async Task<CatalogResponse> GetCatalogItemsAsync(CatalogItemType type, IEnumerable<string> uids, CancellationToken cancellationToken = default)
    {
        var result = new CatalogResponse();
        var endpoint = GetItemEndpoint(type);
        foreach (var uid in uids)
        {
            var itemResult = await GetCatalogItemAsync(type, uid, cancellationToken).ConfigureAwait(false);
            result.Modules.AddRange(itemResult.Modules);
            result.LearningPaths.AddRange(itemResult.LearningPaths);
            result.Courses.AddRange(itemResult.Courses);
            result.Certifications.AddRange(itemResult.Certifications);
            result.MergedCertifications.AddRange(itemResult.MergedCertifications);
            result.AppliedSkills.AddRange(itemResult.AppliedSkills);
            result.Exams.AddRange(itemResult.Exams);
            result.Levels.AddRange(itemResult.Levels);
            result.Roles.AddRange(itemResult.Roles);
            result.Products.AddRange(itemResult.Products);
            result.Subjects.AddRange(itemResult.Subjects);
        }
        return result;
    }
    public async Task<CatalogResponse> GetCatalogItemAsync(CatalogItemType type, string uid, CancellationToken cancellationToken = default)
    {
        var result = new CatalogResponse();
        var endpoint = GetItemEndpoint(type);

        switch (type)
        {
            case CatalogItemType.Module:
            {
                var item = await FetchItemAsync<ModuleRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Modules.Add(item);
                }
                break;
            }
            case CatalogItemType.Unit:
            {
                var item = await FetchItemAsync<UnitRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Units.Add(item);
                }
                break;
            }
            case CatalogItemType.LearningPath:
            {
                var item = await FetchItemAsync<LearningPathRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.LearningPaths.Add(item);
                }
                break;
            }
            case CatalogItemType.AppliedSkill:
            {
                var item = await FetchItemAsync<AppliedSkillRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.AppliedSkills.Add(item);
                }
                break;
            }
            case CatalogItemType.Certification:
            {
                var item = await FetchItemAsync<CertificationRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Certifications.Add(item);
                }
                break;
            }
            case CatalogItemType.MergedCertification:
            {
                var item = await FetchItemAsync<CertificationRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.MergedCertifications.Add(MapMergedCertification(item));
                }
                break;
            }
            case CatalogItemType.Exam:
            {
                var item = await FetchItemAsync<ExamRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Exams.Add(item);
                }
                break;
            }
            case CatalogItemType.Course:
            {
                var item = await FetchItemAsync<CourseRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Courses.Add(item);
                }
                break;
            }
            case CatalogItemType.Level:
            {
                var item = await FetchItemAsync<TaxonomyRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Levels.Add(item);
                }
                break;
            }
            case CatalogItemType.Role:
            {
                var item = await FetchItemAsync<TaxonomyRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Roles.Add(item);
                }
                break;
            }
            case CatalogItemType.Product:
            {
                var item = await FetchItemAsync<TaxonomyRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Products.Add(item);
                }
                break;
            }
            case CatalogItemType.Subject:
            {
                var item = await FetchItemAsync<TaxonomyRecord>(endpoint, uid, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    result.Subjects.Add(item);
                }
                break;
            }
        }

        return result;
    }

    private async Task<T?> FetchItemAsync<T>(string endpoint, string uid, CancellationToken cancellationToken)
    {
        var accessToken = await ResolveAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var requestUri = BuildItemUri(endpoint, uid);
        Console.WriteLine(requestUri);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        logger.LogInformation("Fetching Learn catalog item {Uri}", requestUri);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning(
                "Learn catalog request failed. Endpoint: {Uri}, StatusCode: {StatusCode}, Response: {Response}",
                requestUri,
                (int)response.StatusCode,
                errorBody);
        }

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (TryGetPropertyIgnoreCase(document.RootElement, "value", out var valueElement))
        {
            if (valueElement.ValueKind == JsonValueKind.Array && valueElement.GetArrayLength() > 0)
            {
                return JsonSerializer.Deserialize<T>(valueElement[0].GetRawText(), SerializerOptions);
            }

            if (valueElement.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<T>(valueElement.GetRawText(), SerializerOptions);
            }
        }

        if (document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0)
        {
            return JsonSerializer.Deserialize<T>(document.RootElement[0].GetRawText(), SerializerOptions);
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<T>(document.RootElement.GetRawText(), SerializerOptions);
        }

        return default;
    }

    private async Task<List<T>> FetchPagedAsync<T>(string endpoint, CatalogQuery query, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        string? nextPage = BuildEndpointUri(endpoint, query).ToString();
        var accessToken = await ResolveAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        while (!string.IsNullOrWhiteSpace(nextPage))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextPage);
            request.Headers.Accept.ParseAdd("application/json");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new("Bearer", accessToken);
            }

            logger.LogInformation("Fetching Learn catalog endpoint {Uri}", nextPage);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning(
                    "Learn catalog request failed. Endpoint: {Uri}, StatusCode: {StatusCode}, Response: {Response}",
                    nextPage,
                    (int)response.StatusCode,
                    errorBody);
                //return items;
            }

            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (TryGetPropertyIgnoreCase(document.RootElement, "value", out var valueElement)
                && valueElement.ValueKind == JsonValueKind.Array)
            {
                var pageItems = JsonSerializer.Deserialize<List<T>>(valueElement.GetRawText(), SerializerOptions);
                if (pageItems is { Count: > 0 })
                {
                    items.AddRange(pageItems);
                }
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var pageItems = JsonSerializer.Deserialize<List<T>>(document.RootElement.GetRawText(), SerializerOptions);
                if (pageItems is { Count: > 0 })
                {
                    items.AddRange(pageItems);
                }
            }

            nextPage = TryGetPropertyIgnoreCase(document.RootElement, "nextLink", out var nextLinkElement)
                       && nextLinkElement.ValueKind == JsonValueKind.String
                ? nextLinkElement.GetString()
                : null;
        }

        return items;
    }

    private async Task<string?> ResolveAccessTokenAsync(CancellationToken cancellationToken)
    {
        var scopes = _options.Scopes is { Length: > 0 }
            ? _options.Scopes
            : ["https://learn.microsoft.com/.default"];

        return await accessTokenProvider.GetAccessTokenAsync(scopes, cancellationToken).ConfigureAwait(false);
    }

    private Uri BuildEndpointUri(string endpoint, CatalogQuery query)
    {
        var baseUri = new Uri(_options.BaseUri, endpoint);
        var uriBuilder = new UriBuilder(baseUri);
        query.Type = null;

        uriBuilder.Query = query.ToQueryString(_options.DefaultLocale);

        return uriBuilder.Uri;
    }

    private Uri BuildItemUri(string endpoint, string uid)
    {
        var baseUri = new Uri(_options.BaseUri, $"{endpoint}/{Uri.EscapeDataString(uid)}");
        var uriBuilder = new UriBuilder(baseUri);
        var apiVersion = string.IsNullOrWhiteSpace(_options.ApiVersion)
            ? "2023-11-01-preview"
            : _options.ApiVersion;

        uriBuilder.Query = $"api-version={Uri.EscapeDataString(apiVersion)}";

        return uriBuilder.Uri;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in root.EnumerateObject().Where(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static HashSet<CatalogItemType>? BuildTypeFilter(CatalogItemType[]? types)
        => types is null || types.Length == 0 ? null : [.. types];

    private static bool Includes(HashSet<CatalogItemType>? filter, CatalogItemType type)
        => filter is null || filter.Contains(type);

    private static string GetItemEndpoint(CatalogItemType type) => type switch
    {
        CatalogItemType.Module => "modules",
        CatalogItemType.Unit => "units",
        CatalogItemType.LearningPath => "learning-paths",
        CatalogItemType.AppliedSkill => "applied-skills",
        CatalogItemType.Certification => "certifications",
        CatalogItemType.MergedCertification => "certifications",
        CatalogItemType.Exam => "exams",
        CatalogItemType.Course => "courses",
        CatalogItemType.Level => "levels",
        CatalogItemType.Role => "roles",
        CatalogItemType.Product => "products",
        CatalogItemType.Subject => "subjects",
        _ => type.ToString().ToLowerInvariant()
    };

    private static MergedCertificationRecord MapMergedCertification(CertificationRecord certification)
        => new()
        {
            Uid = certification.Uid,
            Type = certification.Type,
            Title = certification.Title,
            Summary = certification.Subtitle,
            Url = certification.Url,
            IconUrl = certification.IconUrl,
            LastModified = certification.LastModified,
            CertificationType = certification.CertificationType,
            Levels = certification.Levels,
            Roles = certification.Roles,
            Products = certification.Products,
            Subjects = certification.Subjects,
            RenewalFrequencyInDays = certification.RenewalFrequencyInDays,
            Prerequisites = certification.Prerequisites,
            StudyGuide = certification.StudyGuide
        };
}
