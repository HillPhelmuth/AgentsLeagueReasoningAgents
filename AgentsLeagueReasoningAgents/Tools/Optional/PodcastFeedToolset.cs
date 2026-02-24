using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Tools.Optional;

public class PodcastFeedToolset(PodcastFeedService feedService) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(SearchPodcastEpisodesAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Searches Microsoft-focused podcast episode catalogs for exam-relevant technical discussions.")]
    private async Task<string> SearchPodcastEpisodesAsync(
        [Description("Keyword to search against episode titles and descriptions")] string? query = null,
        [Description("Filter to a specific podcast by ID, e.g. 'ms-cloud-it-pro'")] string? podcastId = null,
        [Description("Filter to podcasts relevant to a specific exam code")] string? examCode = null,
        [Description("Number of episodes to return (default: 5, max: 20)")] int maxResults = 5,
        [Description("ISO date string — only return episodes published after this date")] string? afterDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DateTimeOffset? parsedAfterDate = null;
            if (!string.IsNullOrWhiteSpace(afterDate) && DateTimeOffset.TryParse(afterDate, out var parsed))
            {
                parsedAfterDate = parsed;
            }

            var episodes = await feedService.SearchEpisodesAsync(query, podcastId, examCode, maxResults, parsedAfterDate, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(episodes, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }
}