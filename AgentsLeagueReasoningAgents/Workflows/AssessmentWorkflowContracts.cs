using AgentsLeagueReasoningAgents.Models;

namespace AgentsLeagueReasoningAgents.Workflows;

public sealed class AssessmentSessionState
{
    public string StudentEmail { get; set; } = string.Empty;
    public string IntroMessage { get; set; } = string.Empty;
    public List<AssessmentQuestionOutput> Questions { get; set; } = [];
    public Dictionary<string, string> SelectedAnswersByQuestionId { get; set; } = [];
    public int CurrentQuestionIndex { get; set; }
    public bool IsCompleted { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public double ScorePercentage { get; set; }
    public bool IsReadyForExam { get; set; }
    public string? Feedback { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public sealed class AssessmentProgressUpdate
{
    public required AssessmentSessionState Session { get; init; }
    public AssessmentQuestionOutput? CurrentQuestion { get; init; }
    public bool AnswerAccepted { get; init; }
    public string? Message { get; init; }
}
