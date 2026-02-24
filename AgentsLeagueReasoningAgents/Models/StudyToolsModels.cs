namespace AgentsLeagueReasoningAgents.Models;

public record AnswerOption(string Label, string Text, bool IsCorrect);

public record PracticeQuestion(
    string QuestionText,
    List<AnswerOption> Options,
    string CorrectAnswer,
    string? Explanation,
    string ExamCode,
    string SourceUrl);

public record StudyNote(
    string Title,
    string ExamCode,
    string Content,
    string SourceRepo,
    string SourceUrl,
    string? ContentType,
    List<string> Tags);

public record Skill(string Label, List<string> Items);

public record TopicArea(
    string Label,
    int? WeightMin,
    int? WeightMax,
    List<Skill> Skills);

public record ExamSyllabus(
    string ExamCode,
    List<TopicArea> Topics,
    string LastUpdated,
    string FreshnessWarning);

public record FlashCard(
    string Front,
    string Back,
    string ExamCode,
    List<string> Tags,
    string SourceRepo,
    string DeckName);

public record TranscriptSegment(
    double StartSeconds,
    double DurationSeconds,
    string Text);

public record StudyVideo(
    string VideoId,
    string Title,
    string Description,
    string ChannelName,
    string PublishedAt,
    string ThumbnailUrl,
    string VideoUrl,
    TimeSpan? Duration,
    List<TranscriptSegment>? Transcript);

public record QandAPost(
    int QuestionId,
    string Title,
    string QuestionBody,
    int Score,
    List<string> Tags,
    string Url,
    string? TopAnswerBody,
    int? TopAnswerScore,
    bool IsAnswered,
    string Attribution);

public record PodcastEpisode(
    string PodcastName,
    string EpisodeTitle,
    string Description,
    string PublishedDate,
    string? Duration,
    string? AudioUrl,
    string EpisodeUrl,
    List<string> RelevantExams);

public record CommunityResource(
    string Title,
    string Url,
    string Category,
    string ExamCode,
    string SourceRepo,
    string? Description);

public record FreeCertOffer(
    string Provider,
    string Description,
    string Url,
    string? Expiration,
    bool IsExpired,
    bool ExpiringSoon);

public record LabExercise(
    string ExamCode,
    int LabNumber,
    string Title,
    string Content,
    string? EstimatedTime,
    List<string> Objectives,
    string SourceUrl);

public record GitHubFile(string Name, string Path, string Type, string HtmlUrl, string DownloadUrl);

public record GitHubTreeEntry(string Path, string Type, string Sha, string Url);