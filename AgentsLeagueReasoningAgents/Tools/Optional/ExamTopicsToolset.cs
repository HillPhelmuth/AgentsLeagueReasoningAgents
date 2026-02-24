using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Tools.Optional;

public class ExamTopicsToolset(
    GitHubContentService github,
    MarkdownParserService markdownParser) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(SearchFreeCertificationOffersAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Checks for currently available free Microsoft certification exam vouchers and promotional offers.")]
    private async Task<string> SearchFreeCertificationOffersAsync(
        [Description("Vendor to filter by (default: 'Microsoft'). Pass 'all' for all vendors.")] string vendor = "Microsoft",
        [Description("If true, include expired offers for historical context")] bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var offers = new List<FreeCertOffer>();
            offers.AddRange(await ReadOfferTableAsync("README.md", includeExpiredTable: false, cancellationToken).ConfigureAwait(false));
            if (includeExpired)
            {
                offers.AddRange(await ReadOfferTableAsync("Expired-Offers.md", includeExpiredTable: true, cancellationToken).ConfigureAwait(false));
            }

            var filtered = offers
                .Where(offer => string.Equals(vendor, "all", StringComparison.OrdinalIgnoreCase)
                                || offer.Provider.Contains(vendor, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return JsonSerializer.Serialize(filtered, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private async Task<List<FreeCertOffer>> ReadOfferTableAsync(string path, bool includeExpiredTable, CancellationToken cancellationToken)
    {
        var markdown = await github.GetFileContent("cloudcommunity", "Free-Certifications", path, cancellationToken).ConfigureAwait(false);
        var rows = markdownParser.ParseTable(markdown);

        var now = DateTimeOffset.UtcNow;
        var result = new List<FreeCertOffer>();
        foreach (var row in rows)
        {
            var provider = GetValue(row, "Provider");
            var description = GetValue(row, "Description");
            var linkCell = GetValue(row, "Link");
            var expirationText = GetValue(row, "Expiration");
            var link = markdownParser.ExtractLinks(linkCell).FirstOrDefault().url ?? linkCell;
            var expiresAt = DateTimeOffset.TryParse(expirationText, out var parsedExpiration)
                ? parsedExpiration
                : (DateTimeOffset?)null;

            var isExpired = includeExpiredTable || expiresAt is not null && expiresAt < now;
            var expiringSoon = !isExpired && expiresAt is not null && expiresAt <= now.AddDays(7);

            result.Add(new FreeCertOffer(
                Provider: provider,
                Description: description,
                Url: link,
                Expiration: string.IsNullOrWhiteSpace(expirationText) ? null : expirationText,
                IsExpired: isExpired,
                ExpiringSoon: expiringSoon));
        }

        return result;
    }

    private static string GetValue(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value)
            ? value
            : string.Empty;
    }
}