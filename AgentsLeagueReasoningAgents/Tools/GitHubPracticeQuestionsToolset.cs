using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Tools;

public class GitHubPracticeQuestionsToolset(
    GitHubContentService github,
    DitectrevMarkdownParser parser,
    IOptions<DitectrevOptions> ditectrevOptions) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(SearchPracticeQuestionsAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Searches community-maintained practice question banks for Microsoft certification exams and returns multiple-choice questions with answer explanations.")]
    private async Task<string> SearchPracticeQuestionsAsync(
        [Description("Exam code, e.g. 'AZ-900', 'AZ-104', 'SC-900'")] string examCode,
        [Description("Filter questions containing this keyword or phrase")] string? keyword = null,
        [Description("Number of questions to return (default: 5, max: 20)")] int count = 5,
        [Description("If true, return a random selection; if false, return sequentially")] bool random = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedExam = examCode.Trim().ToUpperInvariant();
            if (!ditectrevOptions.Value.Repos.TryGetValue(normalizedExam, out var repoRef) || string.IsNullOrWhiteSpace(repoRef))
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"No configured Ditectrev repository for exam '{normalizedExam}'."
                }, JsonOptions);
            }

            var (owner, repo) = ParseRepo(repoRef);
            var (markdown, sourceUrl) = await github.GetReadmeContent(owner, repo, cancellationToken).ConfigureAwait(false);
            var parsed = parser.ParseQuestions(markdown, normalizedExam, sourceUrl);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                parsed = parsed.Where(question =>
                    question.QuestionText.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || question.Options.Any(option => option.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            var effectiveCount = Math.Clamp(count, 1, 20);
            var selected = random
                ? parsed.OrderBy(_ => Random.Shared.Next()).Take(effectiveCount).ToList()
                : parsed.Take(effectiveCount).ToList();

            return JsonSerializer.Serialize(selected, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = ex.Message
            }, JsonOptions);
        }
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