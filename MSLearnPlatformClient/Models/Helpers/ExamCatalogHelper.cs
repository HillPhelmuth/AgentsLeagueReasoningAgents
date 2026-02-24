using MSLearnPlatformClient.Models.Catalog;

namespace MSLearnPlatformClient.Models.Helpers;

internal static class ExamCatalogHelper
{
    public static ExamRecord? FindExam(IEnumerable<ExamRecord> exams, string examCodeOrUid)
    {
        var normalized = (examCodeOrUid ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var normalizedLower = normalized.ToLowerInvariant();

        return exams.FirstOrDefault(exam =>
            Matches(exam.Uid, normalizedLower)
            || Matches(exam.DisplayName, normalizedLower)
            || Matches(exam.Title, normalizedLower)
            || Matches(exam.Subtitle, normalizedLower));
    }

    private static bool Matches(string? candidate, string normalizedLower)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var lower = candidate.Trim().ToLowerInvariant();
        return lower.Equals(normalizedLower, StringComparison.Ordinal)
            || lower.Contains(normalizedLower, StringComparison.Ordinal)
            || normalizedLower.Contains(lower, StringComparison.Ordinal);
    }
}