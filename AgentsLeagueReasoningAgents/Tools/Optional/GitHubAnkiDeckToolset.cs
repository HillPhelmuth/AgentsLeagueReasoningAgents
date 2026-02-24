using System.ComponentModel;
using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Tools.Optional;

public class GitHubAnkiDeckToolset(AnkiExtractorService ankiExtractor) : IAIToolset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(GetFlashcardsAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Extracts flashcards from open-source Anki decks for spaced-repetition study and returns question-answer pairs.")]
    private async Task<string> GetFlashcardsAsync(
        [Description("Exam code, e.g. 'AZ-104'")] string examCode,
        [Description("Number of flashcards to return (default: 10, max: 50)")] int count = 10,
        [Description("Filter by topic keyword")] string? topic = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var flashcards = await ankiExtractor.GetFlashcardsAsync(examCode, count, topic, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(flashcards, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }
}