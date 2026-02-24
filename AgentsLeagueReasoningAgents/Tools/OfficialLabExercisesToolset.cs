using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Tools;

public class OfficialLabExercisesToolset(
    GitHubContentService github,
    IOptions<OfficialLabsOptions> options) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(GetOfficialLabExercisesAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Retrieves official MIT-licensed hands-on lab instructions from Microsoft Learning GitHub repositories.")]
    private async Task<string> GetOfficialLabExercisesAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Specific lab number to retrieve (e.g. 1 for Lab_01)")] int? labNumber = null,
        [Description("If true, return only the list of available labs without full content")] bool listOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = examCode.Trim().ToUpperInvariant();
            if (!options.Value.RepoMap.TryGetValue(normalized, out var repoRef) || string.IsNullOrWhiteSpace(repoRef))
            {
                return JsonSerializer.Serialize(new { error = $"No MicrosoftLearning repo mapping configured for exam '{normalized}'." }, JsonOptions);
            }

            var (owner, repo) = ParseRepo(repoRef);
            var tree = await github.GetRepoTree(owner, repo, cancellationToken: cancellationToken).ConfigureAwait(false);
            var labFiles = tree
                .Where(entry => entry.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                                && (entry.Path.Contains("Instructions/", StringComparison.OrdinalIgnoreCase)
                                    || entry.Path.Contains("Allfiles/", StringComparison.OrdinalIgnoreCase))
                                && Regex.IsMatch(entry.Path, @"Lab[_-]?(?<num>\d+)", RegexOptions.IgnoreCase))
                .ToList();

            if (labNumber is not null)
            {
                labFiles = labFiles
                    .Where(file => TryGetLabNumber(file.Path, out var number) && number == labNumber.Value)
                    .ToList();
            }

            var labs = new List<LabExercise>();
            foreach (var labFile in labFiles)
            {
                if (!TryGetLabNumber(labFile.Path, out var number))
                {
                    continue;
                }

                var markdown = listOnly
                    ? string.Empty
                    : await github.GetFileContent(owner, repo, labFile.Path, cancellationToken).ConfigureAwait(false);

                var title = Path.GetFileNameWithoutExtension(labFile.Path);
                var estimatedTime = Regex.Match(markdown, @"Estimated\s+Time\s*:\s*(?<time>.+)", RegexOptions.IgnoreCase).Groups["time"].Value;
                var objectives = Regex.Matches(markdown, @"^-\s+(?<objective>.+)$", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(match => match.Groups["objective"].Value.Trim())
                    .Where(objective => !string.IsNullOrWhiteSpace(objective))
                    .Take(8)
                    .ToList();

                labs.Add(new LabExercise(
                    ExamCode: normalized,
                    LabNumber: number,
                    Title: title,
                    Content: markdown,
                    EstimatedTime: string.IsNullOrWhiteSpace(estimatedTime) ? null : estimatedTime,
                    Objectives: objectives,
                    SourceUrl: $"https://github.com/{owner}/{repo}/blob/main/{labFile.Path}"));
            }

            var ordered = labs.OrderBy(lab => lab.LabNumber).ToList();
            return JsonSerializer.Serialize(ordered, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static bool TryGetLabNumber(string path, out int number)
    {
        var match = Regex.Match(path, @"Lab[_-]?(?<num>\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["num"].Value, out number))
        {
            return true;
        }

        number = 0;
        return false;
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