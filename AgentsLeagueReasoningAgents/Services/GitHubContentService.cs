using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Services;

public class GitHubContentService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<GitHubOptions> options,
    ILogger<GitHubContentService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GetFileContent(string owner, string repo, string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:file:{owner}:{repo}:{path}";
        if (memoryCache.TryGetValue<string>(cacheKey, out var cachedContent) && !string.IsNullOrEmpty(cachedContent))
        {
            return cachedContent;
        }

        var client = CreateClient();
        using var response = await client.GetAsync($"repos/{owner}/{repo}/contents/{Uri.EscapeDataString(path)}", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, owner, repo, cancellationToken).ConfigureAwait(false);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var root = document.RootElement;
        var encoding = root.TryGetProperty("encoding", out var encodingElement) ? encodingElement.GetString() : null;
        var content = root.TryGetProperty("content", out var contentElement) ? contentElement.GetString() : null;
        var decoded = string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetString(Convert.FromBase64String((content ?? string.Empty).Replace("\n", string.Empty)))
            : content ?? string.Empty;

        memoryCache.Set(cacheKey, decoded, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return decoded;
    }

    public async Task<List<GitHubFile>> GetDirectoryListing(string owner, string repo, string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:dir:{owner}:{repo}:{path}";
        if (memoryCache.TryGetValue<List<GitHubFile>>(cacheKey, out var cachedListing) && cachedListing is not null)
        {
            return cachedListing;
        }

        var client = CreateClient();
        using var response = await client.GetAsync($"repos/{owner}/{repo}/contents/{Uri.EscapeDataString(path)}", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, owner, repo, cancellationToken).ConfigureAwait(false);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var files = new List<GitHubFile>();
        foreach (var node in document.RootElement.EnumerateArray())
        {
            files.Add(new GitHubFile(
                node.GetProperty("name").GetString() ?? string.Empty,
                node.GetProperty("path").GetString() ?? string.Empty,
                node.GetProperty("type").GetString() ?? string.Empty,
                node.TryGetProperty("html_url", out var htmlUrl) ? htmlUrl.GetString() ?? string.Empty : string.Empty,
                node.TryGetProperty("download_url", out var downloadUrl) ? downloadUrl.GetString() ?? string.Empty : string.Empty));
        }

        memoryCache.Set(cacheKey, files, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return files;
    }

    public async Task<List<GitHubTreeEntry>> GetRepoTree(string owner, string repo, string branch = "main", CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:tree:{owner}:{repo}:{branch}";
        if (memoryCache.TryGetValue<List<GitHubTreeEntry>>(cacheKey, out var cachedTree) && cachedTree is not null)
        {
            return cachedTree;
        }

        var client = CreateClient();
        using var response = await client.GetAsync($"repos/{owner}/{repo}/git/trees/{branch}?recursive=1", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, owner, repo, cancellationToken).ConfigureAwait(false);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var entries = new List<GitHubTreeEntry>();
        foreach (var node in document.RootElement.GetProperty("tree").EnumerateArray())
        {
            entries.Add(new GitHubTreeEntry(
                node.GetProperty("path").GetString() ?? string.Empty,
                node.GetProperty("type").GetString() ?? string.Empty,
                node.TryGetProperty("sha", out var sha) ? sha.GetString() ?? string.Empty : string.Empty,
                node.TryGetProperty("url", out var url) ? url.GetString() ?? string.Empty : string.Empty));
        }

        memoryCache.Set(cacheKey, entries, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return entries;
    }

    public async Task<byte[]> DownloadFile(string owner, string repo, string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:bin:{owner}:{repo}:{path}";
        if (memoryCache.TryGetValue<byte[]>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var client = CreateClient();
        using var metadataResponse = await client.GetAsync($"repos/{owner}/{repo}/contents/{Uri.EscapeDataString(path)}", cancellationToken).ConfigureAwait(false);
        if (!metadataResponse.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(metadataResponse, owner, repo, cancellationToken).ConfigureAwait(false);
        }

        using var metadataDoc = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var downloadUrl = metadataDoc.RootElement.TryGetProperty("download_url", out var downloadUrlElement)
            ? downloadUrlElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException($"File {owner}/{repo}/{path} does not have a download URL.");
        }

        using var bytesResponse = await client.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
        bytesResponse.EnsureSuccessStatusCode();
        var bytes = await bytesResponse.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        memoryCache.Set(cacheKey, bytes, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return bytes;
    }

    public async Task<(int remaining, int limit, DateTimeOffset resetAt)> GetRateLimit(CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await client.GetAsync("rate_limit", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var core = document.RootElement.GetProperty("resources").GetProperty("core");
        var remaining = core.GetProperty("remaining").GetInt32();
        var limit = core.GetProperty("limit").GetInt32();
        var resetUnix = core.GetProperty("reset").GetInt64();
        return (remaining, limit, DateTimeOffset.FromUnixTimeSeconds(resetUnix));
    }

    public async Task<(string content, string sourceUrl)> GetReadmeContent(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:readme:{owner}:{repo}";
        var etagKey = $"github:readme:etag:{owner}:{repo}";

        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{owner}/{repo}/readme");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
        if (memoryCache.TryGetValue<string>(etagKey, out var cachedEtag) && !string.IsNullOrWhiteSpace(cachedEtag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", cachedEtag);
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified && memoryCache.TryGetValue<string>(cacheKey, out var cachedReadme) && !string.IsNullOrEmpty(cachedReadme))
        {
            return (cachedReadme, $"https://github.com/{owner}/{repo}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateGitHubExceptionAsync(response, owner, repo, cancellationToken).ConfigureAwait(false);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.Headers.ETag?.Tag is { Length: > 0 } etag)
        {
            memoryCache.Set(etagKey, etag, TimeSpan.FromHours(24));
        }

        memoryCache.Set(cacheKey, content, TimeSpan.FromMinutes(options.Value.CacheDurationMinutes));
        return (content, $"https://github.com/{owner}/{repo}");
    }

    public async Task<DateTimeOffset?> GetLastCommitDateForPath(string owner, string repo, string path, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await client.GetAsync($"repos/{owner}/{repo}/commits?path={Uri.EscapeDataString(path)}&per_page=1", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Unable to fetch last commit date for {Owner}/{Repo}/{Path}. Status {StatusCode}", owner, repo, path, response.StatusCode);
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var commits = document.RootElement;
        if (commits.ValueKind != JsonValueKind.Array || commits.GetArrayLength() == 0)
        {
            return null;
        }

        var dateString = commits[0].GetProperty("commit").GetProperty("author").GetProperty("date").GetString();
        return DateTimeOffset.TryParse(dateString, out var parsed) ? parsed : null;
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("GitHub");
        client.BaseAddress ??= new Uri("https://api.github.com/");
        if (!client.DefaultRequestHeaders.UserAgent.Any())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AgentsLeagueReasoningAgents/1.0");
        }

        if (!string.IsNullOrWhiteSpace(options.Value.PersonalAccessToken) && client.DefaultRequestHeaders.Authorization is null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.PersonalAccessToken);
        }

        return client;
    }

    private static async Task<Exception> CreateGitHubExceptionAsync(HttpResponseMessage response, string owner, string repo, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new InvalidOperationException($"Repository {owner}/{repo} is no longer available. This resource may have moved.");
        }

        return new HttpRequestException($"GitHub request failed ({(int)response.StatusCode}) for {owner}/{repo}. Body: {body}");
    }
}