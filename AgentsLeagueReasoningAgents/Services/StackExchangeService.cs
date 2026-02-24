using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Services;

public class StackExchangeService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<StackExchangeOptions> options)
{
    private static DateTimeOffset _nextAllowedRequestAt = DateTimeOffset.MinValue;

    public async Task<List<QandAPost>> SearchAsync(
        string query,
        string? examCode,
        int minScore,
        int maxResults,
        bool includeAnswers,
        CancellationToken cancellationToken = default)
    {
        var effectiveMaxResults = Math.Clamp(maxResults, 1, 15);
        var tags = ResolveTags(examCode);
        var cacheKey = $"stack:{query}:{examCode}:{minScore}:{effectiveMaxResults}:{includeAnswers}";
        if (memoryCache.TryGetValue<List<QandAPost>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var waitDuration = _nextAllowedRequestAt - DateTimeOffset.UtcNow;
        if (waitDuration > TimeSpan.Zero)
        {
            await Task.Delay(waitDuration, cancellationToken).ConfigureAwait(false);
        }

        var client = httpClientFactory.CreateClient("StackExchange");
        client.BaseAddress ??= new Uri("https://api.stackexchange.com/2.3/");
        var tagged = string.Join(";", tags);
        var keyPart = string.IsNullOrWhiteSpace(options.Value.AppKey) ? string.Empty : $"&key={Uri.EscapeDataString(options.Value.AppKey)}";
        var url = $"search/advanced?site=stackoverflow&q={Uri.EscapeDataString(query)}&accepted=True&sort=relevance&pagesize={effectiveMaxResults}&filter=withbody{keyPart}";
        if (!string.IsNullOrWhiteSpace(tagged))
        {
            url += $"&tagged={Uri.EscapeDataString(tagged)}";
        }

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var root = document.RootElement;
        if (root.TryGetProperty("backoff", out var backoffElement) && backoffElement.TryGetInt32(out var backoffSeconds))
        {
            _nextAllowedRequestAt = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);
        }

        var posts = new List<QandAPost>();
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            var score = item.TryGetProperty("score", out var scoreElement) ? scoreElement.GetInt32() : 0;
            if (score < minScore)
            {
                continue;
            }

            var questionId = item.GetProperty("question_id").GetInt32();
            string? topAnswerBody = null;
            int? topAnswerScore = null;
            if (includeAnswers)
            {
                (topAnswerBody, topAnswerScore) = await GetTopAnswerAsync(client, questionId, cancellationToken).ConfigureAwait(false);
            }

            posts.Add(new QandAPost(
                QuestionId: questionId,
                Title: item.GetProperty("title").GetString() ?? string.Empty,
                QuestionBody: StripHtml(item.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty),
                Score: score,
                Tags: item.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString() ?? string.Empty).ToList(),
                Url: item.TryGetProperty("link", out var linkElement) ? linkElement.GetString() ?? string.Empty : string.Empty,
                TopAnswerBody: topAnswerBody,
                TopAnswerScore: topAnswerScore,
                IsAnswered: item.TryGetProperty("is_answered", out var answeredElement) && answeredElement.GetBoolean(),
                Attribution: "Content from Stack Overflow, CC BY-SA 4.0"));
        }

        memoryCache.Set(cacheKey, posts, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return posts;
    }

    private async Task<(string? body, int? score)> GetTopAnswerAsync(HttpClient client, int questionId, CancellationToken cancellationToken)
    {
        var keyPart = string.IsNullOrWhiteSpace(options.Value.AppKey) ? string.Empty : $"&key={Uri.EscapeDataString(options.Value.AppKey)}";
        var answerUrl = $"questions/{questionId}/answers?site=stackoverflow&sort=votes&order=desc&pagesize=1&filter=withbody{keyPart}";
        using var response = await client.GetAsync(answerUrl, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return (null, null);
        }

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var items = document.RootElement.GetProperty("items");
        if (items.GetArrayLength() == 0)
        {
            return (null, null);
        }

        var answer = items[0];
        return (
            StripHtml(answer.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty),
            answer.TryGetProperty("score", out var scoreElement) ? scoreElement.GetInt32() : null);
    }

    private List<string> ResolveTags(string? examCode)
    {
        if (string.IsNullOrWhiteSpace(examCode))
        {
            return [];
        }

        return options.Value.ExamTagMap.TryGetValue(examCode, out var tags)
            ? tags
            : [];
    }

    private static string StripHtml(string value)
    {
        var noTags = Regex.Replace(value, "<.*?>", string.Empty);
        return WebUtility.HtmlDecode(noTags).Trim();
    }
}