using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Tools;

public class StackExchangeToolset(StackExchangeService stackExchange) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(SearchStackExchangeAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Searches Stack Overflow for technical Q&A relevant to Microsoft certification exam topics.")]
    private async Task<string> SearchStackExchangeAsync(
        [Description("Search query, e.g. 'Azure VNet peering'")] string query,
        [Description("Exam code — auto-appends relevant tags to the search")] string? examCode = null,
        [Description("Minimum answer score to include (default: 1)")] int minScore = 1,
        [Description("Number of questions to return (default: 5, max: 15)")] int maxResults = 5,
        [Description("If true, include the top-voted answer body for each question")] bool includeAnswers = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await stackExchange.SearchAsync(query, examCode, minScore, maxResults, includeAnswers, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(posts, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }
}