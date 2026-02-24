using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Services;

public class YouTubeService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<YouTubeOptions> options)
{
    private static int _quotaUsed;

    public async Task<List<StudyVideo>> SearchVideosAsync(
        string? examCode,
        string? query,
        string? channelId,
        bool includeTranscript,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var effectiveMaxResults = Math.Clamp(maxResults, 1, 15);
        var cacheKey = $"youtube:{examCode}:{query}:{channelId}:{includeTranscript}:{effectiveMaxResults}";
        if (memoryCache.TryGetValue<List<StudyVideo>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            return [];
        }

        var channels = options.Value.YouTubeChannels.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            channels = channels.Where(channel => string.Equals(channel.Id, channelId, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrWhiteSpace(examCode))
        {
            channels = channels.Where(channel => channel.Exams.Contains(examCode, StringComparer.OrdinalIgnoreCase));
        }

        var searchQuery = string.Join(' ', new[] { examCode, query }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = "Microsoft certification study";
        }

        var results = new List<StudyVideo>();
        foreach (var channel in channels)
        {
            if (results.Count >= effectiveMaxResults)
            {
                break;
            }

            var channelResults = await SearchChannelAsync(channel.Id, channel.Name, searchQuery, effectiveMaxResults - results.Count, cancellationToken).ConfigureAwait(false);
            results.AddRange(channelResults);
        }

        if (includeTranscript)
        {
            foreach (var (video, index) in results.Select((video, index) => (video, index)))
            {
                var transcript = await TryGetTranscriptAsync(video.VideoId, cancellationToken).ConfigureAwait(false);
                results[index] = video with { Transcript = transcript };
            }
        }

        memoryCache.Set(cacheKey, results, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return results.Take(effectiveMaxResults).ToList();
    }

    private async Task<List<StudyVideo>> SearchChannelAsync(string channelId, string channelName, string query, int maxResults, CancellationToken cancellationToken)
    {
        if (maxResults <= 0)
        {
            return [];
        }

        Interlocked.Add(ref _quotaUsed, 100);
        var client = httpClientFactory.CreateClient("YouTube");
        client.BaseAddress ??= new Uri("https://www.googleapis.com/youtube/v3/");
        var url = $"search?part=snippet&channelId={Uri.EscapeDataString(channelId)}&q={Uri.EscapeDataString(query)}&type=video&maxResults={maxResults}&key={Uri.EscapeDataString(options.Value.ApiKey)}";

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var items = document.RootElement.GetProperty("items");
        var list = new List<StudyVideo>();
        foreach (var item in items.EnumerateArray())
        {
            var snippet = item.GetProperty("snippet");
            var videoId = item.GetProperty("id").GetProperty("videoId").GetString() ?? string.Empty;
            list.Add(new StudyVideo(
                VideoId: videoId,
                Title: snippet.GetProperty("title").GetString() ?? string.Empty,
                Description: snippet.GetProperty("description").GetString() ?? string.Empty,
                ChannelName: snippet.TryGetProperty("channelTitle", out var channelTitleElement)
                    ? channelTitleElement.GetString() ?? channelName
                    : channelName,
                PublishedAt: snippet.TryGetProperty("publishedAt", out var publishedAtElement) ? publishedAtElement.GetString() ?? string.Empty : string.Empty,
                ThumbnailUrl: snippet.GetProperty("thumbnails").GetProperty("high").GetProperty("url").GetString() ?? string.Empty,
                VideoUrl: $"https://youtube.com/watch?v={videoId}",
                Duration: null,
                Transcript: null));
        }

        return list;
    }

    private async Task<List<TranscriptSegment>?> TryGetTranscriptAsync(string videoId, CancellationToken cancellationToken)
    {
        var transcriptCacheKey = $"youtube:transcript:{videoId}";
        if (memoryCache.TryGetValue<List<TranscriptSegment>>(transcriptCacheKey, out var cachedTranscript) && cachedTranscript is not null)
        {
            return cachedTranscript;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            var html = await client.GetStringAsync($"https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}", cancellationToken).ConfigureAwait(false);
            var captionsMatch = Regex.Match(html, "\"baseUrl\":\"(?<url>https:[^\"]+timedtext[^\"]*)\"");
            if (!captionsMatch.Success)
            {
                return null;
            }

            var captionsUrl = captionsMatch.Groups["url"].Value.Replace("\\u0026", "&").Replace("\\", string.Empty);
            var timedText = await client.GetStringAsync(captionsUrl, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(timedText))
            {
                return null;
            }

            var segments = Regex.Matches(timedText, @"<text start=""(?<start>[^""]+)"" dur=""(?<dur>[^""]+)"">(?<text>.*?)</text>")
                .Cast<Match>()
                .Select(match => new TranscriptSegment(
                    StartSeconds: double.TryParse(match.Groups["start"].Value, out var start) ? start : 0,
                    DurationSeconds: double.TryParse(match.Groups["dur"].Value, out var duration) ? duration : 0,
                    Text: WebUtility.HtmlDecode(match.Groups["text"].Value)))
                .ToList();

            if (segments.Count == 0)
            {
                return null;
            }

            memoryCache.Set(transcriptCacheKey, segments, TimeSpan.FromHours(1));
            return segments;
        }
        catch
        {
            return null;
        }
    }
}