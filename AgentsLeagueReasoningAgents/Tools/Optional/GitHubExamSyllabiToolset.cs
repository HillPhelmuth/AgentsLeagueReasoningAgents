using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Tools.Optional;

public class GitHubExamSyllabiToolset(GitHubContentService github) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string FreshnessWarning = "This syllabus data is from 2019 and may be outdated. Always cross-reference against the latest Microsoft Learn catalog skill outline.";

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(GetExamSyllabusAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Returns structured exam syllabus with topic areas, percentage weights, and skill breakdowns. Data may be outdated and should be cross-referenced with Microsoft Learn.")]
    private async Task<string> GetExamSyllabusAsync(
        [Description("Exam code, e.g. 'AZ-900'")] string examCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = examCode.Trim().ToUpperInvariant();
            var paths = new[] { $"exams/{normalized}.json", $"exams/{normalized.ToLowerInvariant()}.json" };
            string? json = null;
            string? path = null;
            foreach (var candidatePath in paths)
            {
                try
                {
                    json = await github.GetFileContent("FurkanKambay", "ms-cert-exams-json", candidatePath, cancellationToken).ConfigureAwait(false);
                    path = candidatePath;
                    break;
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
            {
                return JsonSerializer.Serialize(new { error = $"Syllabus file not found for exam '{normalized}'.", freshnessWarning = FreshnessWarning }, JsonOptions);
            }

            using var document = JsonDocument.Parse(json);
            var topicNodes = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToList()
                : [document.RootElement];

            var topics = new List<TopicArea>();
            foreach (var topicNode in topicNodes)
            {
                var label = topicNode.TryGetProperty("label", out var labelElement) ? labelElement.GetString() ?? string.Empty : string.Empty;
                var (min, max) = ParseWeight(label);
                var skills = new List<Skill>();
                if (topicNode.TryGetProperty("skills", out var skillsNode) && skillsNode.ValueKind == JsonValueKind.Array)
                {
                    foreach (var skillNode in skillsNode.EnumerateArray())
                    {
                        var skillLabel = skillNode.TryGetProperty("label", out var skillLabelElement) ? skillLabelElement.GetString() ?? string.Empty : string.Empty;
                        var items = skillNode.TryGetProperty("items", out var itemsNode) && itemsNode.ValueKind == JsonValueKind.Array
                            ? itemsNode.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList()
                            : [];
                        skills.Add(new Skill(skillLabel, items));
                    }
                }

                topics.Add(new TopicArea(label, min, max, skills));
            }

            var lastUpdated = await github.GetLastCommitDateForPath("FurkanKambay", "ms-cert-exams-json", path, cancellationToken).ConfigureAwait(false);
            var output = new ExamSyllabus(
                ExamCode: normalized,
                Topics: topics,
                LastUpdated: (lastUpdated ?? DateTimeOffset.Parse("2019-01-01")).ToString("O"),
                FreshnessWarning: FreshnessWarning);

            return JsonSerializer.Serialize(output, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message, freshnessWarning = FreshnessWarning }, JsonOptions);
        }
    }

    private static (int? min, int? max) ParseWeight(string label)
    {
        var range = Regex.Match(label, @"\((?<min>\d+)-(?<max>\d+)%\)");
        if (range.Success)
        {
            return (int.Parse(range.Groups["min"].Value), int.Parse(range.Groups["max"].Value));
        }

        var single = Regex.Match(label, @"\((?<value>\d+)%\)");
        if (single.Success)
        {
            var value = int.Parse(single.Groups["value"].Value);
            return (value, value);
        }

        return (null, null);
    }
}