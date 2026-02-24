using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Tools.Optional;

public class GitHubStudyNotesToolset(
    GitHubContentService github,
    MarkdownParserService markdownParser,
    IOptions<StudyNotesOptions> studyNotesOptions) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(GetStudyNotesAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Retrieves structured Markdown study notes for a Microsoft certification exam topic from community GitHub repositories.")]
    private async Task<string> GetStudyNotesAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Topic keyword to filter notes, e.g. 'networking', 'identity', 'storage'")] string? topic = null,
        [Description("Specific repo source ID, e.g. 'bullet-points'. Defaults to all available.")] string? source = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedExam = examCode.Trim().ToUpperInvariant();
            var sources = studyNotesOptions.Value.Repos
                .Where(repo => repo.Exams.Contains(normalizedExam, StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(source))
            {
                sources = sources.Where(repo => string.Equals(repo.Id, source, StringComparison.OrdinalIgnoreCase));
            }

            var notes = new List<StudyNote>();
            foreach (var repoSource in sources)
            {
                var (owner, repo) = ParseRepo(repoSource.Repo);
                var tree = await github.GetRepoTree(owner, repo, cancellationToken: cancellationToken).ConfigureAwait(false);

                var examPathPrefix = repoSource.PathPattern?.Replace("{code}", normalizedExam.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase)
                                  ?? normalizedExam;
                var markdownFiles = tree
                    .Where(entry => string.Equals(entry.Type, "blob", StringComparison.OrdinalIgnoreCase)
                                    && entry.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                                    && (entry.Path.Contains(normalizedExam, StringComparison.OrdinalIgnoreCase)
                                        || entry.Path.Contains(examPathPrefix, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (!string.IsNullOrWhiteSpace(topic))
                {
                    var pathMatches = markdownFiles.Where(entry => entry.Path.Contains(topic, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (pathMatches.Count > 0)
                    {
                        markdownFiles = pathMatches;
                    }
                }

                foreach (var file in markdownFiles.Take(15))
                {
                    var content = await github.GetFileContent(owner, repo, file.Path, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(topic)
                        && !file.Path.Contains(topic, StringComparison.OrdinalIgnoreCase)
                        && !content.Contains(topic, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var tags = new List<string>();
                    if (content.Contains("💡", StringComparison.Ordinal)) tags.Add("best-practice");
                    if (content.Contains("❗", StringComparison.Ordinal)) tags.Add("limitation");
                    if (content.Contains("📝", StringComparison.Ordinal)) tags.Add("exam-topic");
                    tags.AddRange(GetPathTags(file.Path));
                    tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    var sections = markdownParser.ExtractSections(content, 1);
                    var title = sections.Keys.FirstOrDefault()
                                ?? Path.GetFileNameWithoutExtension(file.Path);

                    notes.Add(new StudyNote(
                        Title: title,
                        ExamCode: normalizedExam,
                        Content: content,
                        SourceRepo: repoSource.Repo,
                        SourceUrl: $"https://github.com/{owner}/{repo}/blob/main/{file.Path}",
                        ContentType: ResolveContentType(file.Path),
                        Tags: tags));
                }
            }

            return JsonSerializer.Serialize(notes, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static string? ResolveContentType(string path)
    {
        if (path.Contains("Case Studies", StringComparison.OrdinalIgnoreCase)) return "case-study";
        if (path.Contains("Exercises", StringComparison.OrdinalIgnoreCase)) return "exercise";
        if (path.Contains("Knowledge Checks", StringComparison.OrdinalIgnoreCase)) return "knowledge-check";
        if (path.Contains("Questions", StringComparison.OrdinalIgnoreCase)) return "practice-question";
        return "notes";
    }

    private static IEnumerable<string> GetPathTags(string path)
    {
        if (path.Contains("Topics", StringComparison.OrdinalIgnoreCase)) yield return "topic";
        if (path.Contains("Questions", StringComparison.OrdinalIgnoreCase)) yield return "questions";
        if (path.Contains("Case Studies", StringComparison.OrdinalIgnoreCase)) yield return "case-study";
        if (path.Contains("Exercises", StringComparison.OrdinalIgnoreCase)) yield return "exercise";
        if (path.Contains("Knowledge Checks", StringComparison.OrdinalIgnoreCase)) yield return "knowledge-check";
    }

    private static (string owner, string repo) ParseRepo(string value)
    {
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Repository value '{value}' must be 'owner/repo'.", nameof(value));
        }

        return (parts[0], parts[1]);
    }
}