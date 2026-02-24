using System.ComponentModel;
using AgentsLeagueReasoningAgents.Models;

namespace AgentsLeagueReasoningAgents.Workflows;



public sealed record PreparationWorkflowRequest(
    string Topics,
    string StudentEmail,
    int WeeklyHours,
    int DurationWeeks, bool UseSingleAgent = false)
{
    public string AsMarkdown()
    {
        return $"""
                **User email:** {StudentEmail}
                **Learn topics:** {Topics}
                **Weekly study hours:** {WeeklyHours}
                **Duration in weeks:** {DurationWeeks}
                **Today's date (UTC):** {DateTimeOffset.UtcNow:d}
               
                Produce JSON only matching the provided schema

                """;
    }
}

public sealed class PreparationWorkflowResult
{
    public PreparationWorkflowResult(LearningPathCurationOutput? curatedLearningPathStructured,
        StudyPlanOutput? studyPlanStructured,
        EngagementPlanOutput? engagementPlanStructured,
        string? workflowTranscript)
    {
        CuratedLearningPathStructured = curatedLearningPathStructured;
        StudyPlanStructured = studyPlanStructured;
        EngagementPlanStructured = engagementPlanStructured;
        WorkflowTranscript = workflowTranscript;
    }

    public PreparationWorkflowResult()
    {

    }
    [Description("Curated Learning Path")]
    public LearningPathCurationOutput? CuratedLearningPathStructured { get; set; }
    [Description("Study Plan for Microsoft Certification")]
    public StudyPlanOutput? StudyPlanStructured { get; set; }
    [Description("Engagement Plan with reminders and motivational messages")]
    public EngagementPlanOutput? EngagementPlanStructured { get; set; }

    [Description("Student email used as state key for preparation and assessment flows")]
    public string? StudentEmail { get; set; }

    [Description("UTC timestamp when preparation output was generated")]
    public DateTimeOffset? PreparationCompletedAtUtc { get; set; }

    public string? WorkflowTranscript { get; set; }

    
}