using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Tools;

public class GitHubCommunityHubToolset(
    GitHubContentService github,
    MarkdownParserService markdownParser) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(GetCommunityResourcesAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Returns curated community resource links for a Microsoft certification exam, including courses, labs, practice tests, videos, and study guides.")]
    private async Task<string> GetCommunityResourcesAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Resource type filter: 'courses', 'labs', 'practice-tests', 'videos', 'study-guides', or 'all'")] string resourceType = "all",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = examCode.Trim().ToUpperInvariant();
            var resources = new List<CommunityResource>();

            resources.AddRange(await ReadResourcesAsync(
                owner: "mscerts",
                repo: "hub",
                candidatePaths:
                [
                    $"docs/azure/{normalized}.md",
                    $"docs/security/{normalized}.md",
                    $"docs/{normalized}.md"
                ],
                examCode: normalized,
                cancellationToken: cancellationToken).ConfigureAwait(false));

            resources.AddRange(await ReadResourcesAsync(
                owner: "shiftavenue",
                repo: "awesome-azure-learning",
                candidatePaths:
                [
                    $"topics/certifications/{normalized}.md",
                    $"topics/certifications/{normalized.ToLowerInvariant()}.md"
                ],
                examCode: normalized,
                cancellationToken: cancellationToken).ConfigureAwait(false));

            var filtered = resources
                .GroupBy(resource => resource.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Where(resource => string.Equals(resourceType, "all", StringComparison.OrdinalIgnoreCase)
                                   || resource.Category.Contains(resourceType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return JsonSerializer.Serialize(filtered, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private async Task<List<CommunityResource>> ReadResourcesAsync(
        string owner,
        string repo,
        IEnumerable<string> candidatePaths,
        string examCode,
        CancellationToken cancellationToken)
    {
        string? markdown = null;
        string? path = null;
        foreach (var candidatePath in candidatePaths)
        {
            try
            {
                markdown = await github.GetFileContent(owner, repo, candidatePath, cancellationToken).ConfigureAwait(false);
                path = candidatePath;
                break;
            }
            catch
            {
            }
        }

        if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var sections = markdownParser.ExtractSections(markdown, 2);
        var resources = new List<CommunityResource>();
        foreach (var (header, content) in sections)
        {
            var links = markdownParser.ExtractLinks(content);
            var category = MapCategory(header);
            foreach (var (title, url) in links)
            {
                resources.Add(new CommunityResource(
                    Title: title,
                    Url: url,
                    Category: category,
                    ExamCode: examCode,
                    SourceRepo: $"{owner}/{repo}",
                    Description: null));
            }
        }

        return resources;
    }

    private static string MapCategory(string sectionHeader)
    {
        if (sectionHeader.Contains("lab", StringComparison.OrdinalIgnoreCase)) return "lab";
        if (sectionHeader.Contains("practice", StringComparison.OrdinalIgnoreCase)) return "practice-test";
        if (sectionHeader.Contains("video", StringComparison.OrdinalIgnoreCase)) return "video";
        if (sectionHeader.Contains("study", StringComparison.OrdinalIgnoreCase)) return "study-guide";
        if (sectionHeader.Contains("course", StringComparison.OrdinalIgnoreCase)) return "course";
        return "blog-post";
    }
}