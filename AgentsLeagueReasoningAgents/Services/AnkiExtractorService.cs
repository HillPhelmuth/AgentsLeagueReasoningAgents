using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AgentsLeagueReasoningAgents.Services;

public class AnkiExtractorService(
    GitHubContentService gitHubContentService,
    IMemoryCache memoryCache,
    IOptions<AnkiDeckOptions> options)
{
    public async Task<List<FlashCard>> GetFlashcardsAsync(string examCode, int count, string? topic, CancellationToken cancellationToken = default)
    {
        var effectiveCount = Math.Clamp(count, 1, 50);
        var cacheKey = $"anki:{examCode}:{topic}:{effectiveCount}";
        if (memoryCache.TryGetValue<List<FlashCard>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var matchingDecks = options.Value.AnkiDecks
            .Where(deck => string.Equals(deck.Exam, examCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allCards = new List<FlashCard>();
        foreach (var deck in matchingDecks)
        {
            var cards = string.Equals(deck.Format, "crowdanki", StringComparison.OrdinalIgnoreCase)
                ? await ReadCrowdAnkiDeckAsync(deck, cancellationToken).ConfigureAwait(false)
                : await ReadApkgDeckAsync(deck, cancellationToken).ConfigureAwait(false);
            allCards.AddRange(cards);
        }

        var filtered = allCards
            .Where(card => string.IsNullOrWhiteSpace(topic)
                           || card.Front.Contains(topic, StringComparison.OrdinalIgnoreCase)
                           || card.Back.Contains(topic, StringComparison.OrdinalIgnoreCase)
                           || card.Tags.Any(tag => tag.Contains(topic, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(_ => Random.Shared.Next())
            .Take(effectiveCount)
            .ToList();

        memoryCache.Set(cacheKey, filtered, TimeSpan.FromHours(1));
        return filtered;
    }

    private async Task<List<FlashCard>> ReadApkgDeckAsync(AnkiDeckConfig config, CancellationToken cancellationToken)
    {
        var (owner, repo) = ParseRepo(config.Repo);
        var filePath = config.Path;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            var tree = await gitHubContentService.GetRepoTree(owner, repo, cancellationToken: cancellationToken).ConfigureAwait(false);
            filePath = tree.FirstOrDefault(entry => entry.Path.EndsWith(".apkg", StringComparison.OrdinalIgnoreCase))?.Path
                       ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return [];
        }

        var bytes = await gitHubContentService.DownloadFile(owner, repo, filePath, cancellationToken).ConfigureAwait(false);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var dbEntry = archive.GetEntry("collection.anki2") ?? archive.GetEntry("collection.anki21");
        if (dbEntry is null)
        {
            return [];
        }

        var tempPath = Path.GetTempFileName();
        try
        {
            await using (var dbStream = dbEntry.Open())
            await using (var fileStream = File.OpenWrite(tempPath))
            {
                await dbStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            var cards = new List<FlashCard>();
            await using var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT flds, tags FROM notes";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var fields = (reader.GetString(0) ?? string.Empty).Split('\x1f');
                var tags = (reader.GetString(1) ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (fields.Length < 2)
                {
                    continue;
                }

                cards.Add(new FlashCard(
                    Front: StripHtml(fields[0]),
                    Back: StripHtml(fields[1]),
                    ExamCode: config.Exam,
                    Tags: tags,
                    SourceRepo: config.Repo,
                    DeckName: repo));
            }

            return cards;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private async Task<List<FlashCard>> ReadCrowdAnkiDeckAsync(AnkiDeckConfig config, CancellationToken cancellationToken)
    {
        var (owner, repo) = ParseRepo(config.Repo);
        var deckPath = config.Path;
        if (string.IsNullOrWhiteSpace(deckPath))
        {
            var tree = await gitHubContentService.GetRepoTree(owner, repo, cancellationToken: cancellationToken).ConfigureAwait(false);
            deckPath = tree.FirstOrDefault(entry => entry.Path.EndsWith("deck.json", StringComparison.OrdinalIgnoreCase))?.Path
                       ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(deckPath))
        {
            return [];
        }

        var json = await gitHubContentService.GetFileContent(owner, repo, deckPath, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var deckName = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? repo : repo;

        var cards = new List<FlashCard>();
        if (!root.TryGetProperty("notes", out var notesElement) || notesElement.ValueKind != JsonValueKind.Array)
        {
            return cards;
        }

        foreach (var note in notesElement.EnumerateArray())
        {
            if (!note.TryGetProperty("fields", out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Array || fieldsElement.GetArrayLength() < 2)
            {
                continue;
            }

            var front = StripHtml(fieldsElement[0].GetString() ?? string.Empty);
            var back = StripHtml(fieldsElement[1].GetString() ?? string.Empty);
            var tags = note.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array
                ? tagsElement.EnumerateArray().Select(tag => tag.GetString() ?? string.Empty).Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList()
                : [];

            cards.Add(new FlashCard(front, back, config.Exam, tags, config.Repo, deckName));
        }

        return cards;
    }

    private static (string owner, string repo) ParseRepo(string ownerAndRepo)
    {
        var parts = ownerAndRepo.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Repository must be formatted as owner/repo. Received '{ownerAndRepo}'.", nameof(ownerAndRepo));
        }

        return (parts[0], parts[1]);
    }

    private static string StripHtml(string value)
    {
        var noTags = Regex.Replace(value, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(noTags).Trim();
    }
}