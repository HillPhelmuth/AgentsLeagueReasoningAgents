namespace AgentsLeagueReasoningAgents.Options;

public sealed class GitHubOptions
{
    public string? PersonalAccessToken { get; set; }
    public int RateLimitPerHour { get; set; } = 5000;
    public int CacheDurationMinutes { get; set; } = 60;
}

public sealed class DitectrevOptions
{
    public Dictionary<string, string> Repos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StudyNoteSource
{
    public required string Id { get; set; }
    public required string Repo { get; set; }
    public List<string> Exams { get; set; } = [];
    public string? Description { get; set; }
    public string? License { get; set; }
    public string? PathPattern { get; set; }
}

public sealed class StudyNotesOptions
{
    public List<StudyNoteSource> Repos { get; set; } = [];
}

public sealed class AnkiDeckConfig
{
    public required string Repo { get; set; }
    public required string Exam { get; set; }
    public required string Format { get; set; }
    public string Path { get; set; } = string.Empty;
    public int? CardCount { get; set; }
    public string? Description { get; set; }
}

public sealed class AnkiDeckOptions
{
    public List<AnkiDeckConfig> AnkiDecks { get; set; } = [];
}

public sealed class YouTubeChannelOption
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public List<string> Exams { get; set; } = [];
    public string? Notes { get; set; }
}

public sealed class YouTubeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public int DailyQuotaUnits { get; set; } = 10000;
    public int CacheDurationMinutes { get; set; } = 60;
    public List<YouTubeChannelOption> YouTubeChannels { get; set; } = [];
}

public sealed class StackExchangeOptions
{
    public string? AppKey { get; set; }
    public int CacheDurationMinutes { get; set; } = 15;
    public Dictionary<string, List<string>> ExamTagMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PodcastFeedOption
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string FeedUrl { get; set; }
    public List<string> RelevantExams { get; set; } = [];
    public string? Description { get; set; }
}

public sealed class PodcastOptions
{
    public int CacheDurationMinutes { get; set; } = 30;
    public List<PodcastFeedOption> PodcastFeeds { get; set; } = [];
}

public sealed class OfficialLabsOptions
{
    public Dictionary<string, string> RepoMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}