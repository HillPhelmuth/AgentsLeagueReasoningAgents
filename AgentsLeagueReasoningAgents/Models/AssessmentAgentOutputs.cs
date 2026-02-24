using System.ComponentModel;

namespace AgentsLeagueReasoningAgents.Models;

public sealed class AssessmentQuestionSetOutput
{
    [Description("Friendly introduction shown before the assessment starts.")]
    public string IntroMessage { get; init; } = string.Empty;

    [Description("Multiple-choice questions tailored to the student's preparation plan.")]
    public List<AssessmentQuestionOutput> Questions { get; init; } = [];
}

public sealed class AssessmentQuestionOutput
{
    [Description("Unique question id for answer tracking.")]
    public string QuestionId { get; init; } = string.Empty;

    [Description("Question text presented to the student.")]
    public string Prompt { get; init; } = string.Empty;

    [Description("Ordered answer options.")]
    public List<AssessmentOptionOutput> Options { get; init; } = [];

    [Description("Option id that is considered correct.")]
    public string CorrectOptionId { get; init; } = string.Empty;

    [Description("Brief explanation for the correct answer.")]
    public string Explanation { get; init; } = string.Empty;
}

public sealed class AssessmentOptionOutput
{
    [Description("Option id, typically A/B/C/D.")]
    public string OptionId { get; init; } = string.Empty;

    [Description("Answer option text.")]
    public string Text { get; init; } = string.Empty;
}
