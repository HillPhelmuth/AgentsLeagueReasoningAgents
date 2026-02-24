using System.Net;
using System.Text.RegularExpressions;
using AgentsLeagueReasoningAgents.Models;

namespace AgentsLeagueReasoningAgents.Services;

public class MarkdownParserService
{
    private static readonly Regex LinkRegex = new(@"\[(?<title>[^\]]+)\]\((?<url>[^\)]+)\)", RegexOptions.Compiled);
    private static readonly Regex HeaderRegex = new(@"^(?<hashes>#+)\s+(?<title>.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled);

    public List<(string title, string url)> ExtractLinks(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        return LinkRegex.Matches(markdown)
            .Select(match => (match.Groups["title"].Value.Trim(), match.Groups["url"].Value.Trim()))
            .ToList();
    }

    public Dictionary<string, string> ExtractSections(string markdown, int headerLevel = 2)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return result;
        }

        var matches = HeaderRegex.Matches(markdown).Cast<Match>().ToList();
        for (var i = 0; i < matches.Count; i++)
        {
            var current = matches[i];
            var level = current.Groups["hashes"].Value.Length;
            if (level != headerLevel)
            {
                continue;
            }

            var start = current.Index + current.Length;
            var end = markdown.Length;
            for (var j = i + 1; j < matches.Count; j++)
            {
                if (matches[j].Groups["hashes"].Value.Length <= headerLevel)
                {
                    end = matches[j].Index;
                    break;
                }
            }

            var title = current.Groups["title"].Value.Trim();
            var body = markdown[start..end].Trim();
            result[title] = body;
        }

        return result;
    }

    public List<Dictionary<string, string>> ParseTable(string markdown)
    {
        var rows = new List<Dictionary<string, string>>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return rows;
        }

        var lines = markdown.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith('|')).ToList();
        if (lines.Count < 2)
        {
            return rows;
        }

        var headers = SplitTableLine(lines[0]);
        foreach (var line in lines.Skip(2))
        {
            var values = SplitTableLine(line);
            if (values.Count == 0)
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                row[headers[i]] = i < values.Count ? values[i] : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    public string StripFormatting(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var text = LinkRegex.Replace(markdown, "$1");
        text = Regex.Replace(text, @"^\s{0,3}#{1,6}\s*", string.Empty, RegexOptions.Multiline);
        text = Regex.Replace(text, @"[*_~`>-]", string.Empty);
        text = HtmlTagRegex.Replace(text, string.Empty);
        return WebUtility.HtmlDecode(text).Trim();
    }

    public List<PracticeQuestion> ParseDitectrevQuestions(string markdown, string examCode)
    {
        var results = new List<PracticeQuestion>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return results;
        }

        var blocks = markdown.Split("### ", StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawBlock in blocks)
        {
            var block = rawBlock.Replace("\r", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(block))
            {
                continue;
            }

            var firstOptionIndex = block.IndexOf("- [", StringComparison.Ordinal);
            if (firstOptionIndex <= 0)
            {
                continue;
            }

            var questionText = block[..firstOptionIndex].Trim();
            questionText = Regex.Replace(questionText, @"!\[[^\]]*\]\([^\)]*\)", "[Image removed — see source]");

            var optionMatches = Regex.Matches(block, @"^- \[(?<mark>[xX ])\]\s*(?<text>.+)$", RegexOptions.Multiline);
            if (optionMatches.Count == 0)
            {
                continue;
            }

            var options = new List<AnswerOption>();
            var labels = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            foreach (var (match, index) in optionMatches.Cast<Match>().Select((m, i) => (m, i)))
            {
                var optionText = match.Groups["text"].Value.Trim();
                if (optionText.Contains("Back to Top", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isCorrect = string.Equals(match.Groups["mark"].Value, "x", StringComparison.OrdinalIgnoreCase);
                var label = index < labels.Length ? labels[index].ToString() : (index + 1).ToString();
                options.Add(new AnswerOption(label, optionText, isCorrect));
            }

            if (options.Count == 0)
            {
                continue;
            }

            var correct = options.Where(option => option.IsCorrect).Select(option => option.Label).ToList();
            var explanation = ExtractSections(block, 4).TryGetValue("Explanation", out var section)
                ? section
                : null;

            results.Add(new PracticeQuestion(
                QuestionText: questionText,
                Options: options,
                CorrectAnswer: correct.Count > 0 ? string.Join(", ", correct) : "Unknown",
                Explanation: explanation,
                ExamCode: examCode,
                SourceUrl: string.Empty));
        }

        return results;
    }

    private static List<string> SplitTableLine(string line)
    {
        return line.Trim('|')
            .Split('|')
            .Select(value => value.Trim())
            .ToList();
    }
}