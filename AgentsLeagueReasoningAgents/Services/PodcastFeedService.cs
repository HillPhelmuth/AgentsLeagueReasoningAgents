using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Services;

public class PodcastFeedService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<PodcastOptions> options,
    ILogger<PodcastFeedService> logger)
{
    public async Task<List<PodcastEpisode>> SearchEpisodesAsync(
        string? query,
        string? podcastId,
        string? examCode,
        int maxResults,
        DateTimeOffset? afterDate,
        CancellationToken cancellationToken = default)
    {
        var feeds = options.Value.PodcastFeeds.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(podcastId))
        {
            feeds = feeds.Where(feed => string.Equals(feed.Id, podcastId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(examCode))
        {
            feeds = feeds.Where(feed => feed.RelevantExams.Contains(examCode, StringComparer.OrdinalIgnoreCase));
        }

        var allEpisodes = new List<PodcastEpisode>();
        foreach (var feed in feeds)
        {
            try
            {
                allEpisodes.AddRange(await GetFeedEpisodesAsync(feed, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load podcast feed {FeedId}", feed.Id);
            }
        }

        var filtered = allEpisodes
            .Where(episode => string.IsNullOrWhiteSpace(query)
                || episode.EpisodeTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                || episode.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(episode => afterDate is null || DateTimeOffset.TryParse(episode.PublishedDate, out var published) && published >= afterDate)
            .OrderByDescending(episode => DateTimeOffset.TryParse(episode.PublishedDate, out var published) ? published : DateTimeOffset.MinValue)
            .Take(Math.Clamp(maxResults, 1, 20))
            .ToList();

        return filtered;
    }

    private async Task<List<PodcastEpisode>> GetFeedEpisodesAsync(PodcastFeedOption feed, CancellationToken cancellationToken)
    {
        var cacheKey = $"podcast-feed:{feed.Id}";
        if (memoryCache.TryGetValue<List<PodcastEpisode>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var client = httpClientFactory.CreateClient();
        using var stream = await client.GetStreamAsync(feed.FeedUrl, cancellationToken).ConfigureAwait(false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        var syndicationFeed = SyndicationFeed.Load(reader);

        var episodes = new List<PodcastEpisode>();
        foreach (var item in syndicationFeed.Items)
        {
            var description = item.Summary?.Text ?? item.Content?.ToString() ?? string.Empty;
            var plainDescription = Regex.Replace(description, "<.*?>", string.Empty).Trim();
            if (plainDescription.Length > 500)
            {
                plainDescription = plainDescription[..500];
            }

            var enclosure = item.Links.FirstOrDefault(link => string.Equals(link.RelationshipType, "enclosure", StringComparison.OrdinalIgnoreCase));
            var webLink = item.Links.FirstOrDefault(link => string.Equals(link.RelationshipType, "alternate", StringComparison.OrdinalIgnoreCase))
                         ?? item.Links.FirstOrDefault();

            var duration = item.ElementExtensions
                .Where(extension => string.Equals(extension.OuterName, "duration", StringComparison.OrdinalIgnoreCase))
                .Select(extension => extension.GetObject<string>())
                .FirstOrDefault();

            episodes.Add(new PodcastEpisode(
                PodcastName: feed.Name,
                EpisodeTitle: item.Title?.Text ?? string.Empty,
                Description: plainDescription,
                PublishedDate: item.PublishDate.UtcDateTime == DateTime.MinValue
                    ? DateTimeOffset.MinValue.ToString("O")
                    : item.PublishDate.ToString("O"),
                Duration: duration,
                AudioUrl: enclosure?.Uri?.ToString(),
                EpisodeUrl: webLink?.Uri?.ToString() ?? string.Empty,
                RelevantExams: feed.RelevantExams));
        }

        memoryCache.Set(cacheKey, episodes, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return episodes;
    }
}