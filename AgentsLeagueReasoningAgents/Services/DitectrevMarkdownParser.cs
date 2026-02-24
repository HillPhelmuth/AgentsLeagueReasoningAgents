using AgentsLeagueReasoningAgents.Models;

namespace AgentsLeagueReasoningAgents.Services;

public sealed class DitectrevMarkdownParser(MarkdownParserService markdownParserService)
{
    public List<PracticeQuestion> ParseQuestions(string markdown, string examCode, string sourceUrl)
    {
        var parsed = markdownParserService.ParseDitectrevQuestions(markdown, examCode);
        return parsed.Select(question => question with { SourceUrl = sourceUrl }).ToList();
    }
}