using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Tools.Optional;

public class YouTubeStudyContentToolset(YouTubeService youtube) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(SearchYouTubeStudyContentAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Searches free Microsoft Certification study videos on YouTube and optionally retrieves transcripts.")]
    private async Task<string> SearchYouTubeStudyContentAsync(
        [Description("Exam code to filter results, e.g. 'AZ-104'")] string? examCode = null,
        [Description("Search query within the curated channel set")] string? query = null,
        [Description("Limit results to a specific channel ID")] string? channelId = null,
        [Description("If true, fetch and include the video transcript (quota-heavy; use sparingly)")] bool includeTranscript = false,
        [Description("Number of videos to return (default: 5, max: 15)")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var videos = await youtube.SearchVideosAsync(examCode, query, channelId, includeTranscript, maxResults, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(videos, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }
}