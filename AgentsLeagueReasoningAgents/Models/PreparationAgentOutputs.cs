using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;

namespace AgentsLeagueReasoningAgents.Models;

public sealed class LearningPathCurationOutput
{
    [Description("Short rationale explaining why these recommendations fit the student goals.")]
    public string Rationale { get; init; } = string.Empty;

    [Description("Ordered list of recommended Microsoft Learn learning paths.")]
    public List<LearningPathRecommendation> LearningPaths { get; init; } = [];

    [Description("Supporting module recommendations to reinforce weak areas.")]
    public List<ModuleRecommendation> Modules { get; init; } = [];

    [Description("Potential certifications, applied skills or exams related to the selected topic area.")]
    public List<CertificationOrExamRecommendation> Targets { get; init; } = [];

    public string AsMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"**Rationale:** {Rationale}");
        builder.AppendLine("### Learning Path Recommendations:");
        builder.AppendLine();
        foreach (var path in LearningPaths)
        {
            builder.AppendLine($"- [{path.Title}]({path.Url}) - Estimated hours: {path.EstimatedHours}. Relevance: {path.Relevance}");
        }
        builder.AppendLine();
        builder.AppendLine("### Module Recommendations:");
        builder.AppendLine();
        foreach (var module in Modules)
        {
            builder.AppendLine($"- [{module.Title}]({module.Url}) - Skill focus: {module.SkillFocus}");
            builder.AppendLine();
             foreach (var unit in module.ModuleUnits)
            {
                builder.AppendLine($"  - [{unit.Title}]({unit.Url}) - Duration: {unit.DurationInMinutes} minutes");
            }
        }
        builder.AppendLine();
        builder.AppendLine("### Certification/Exam Targets:");
        builder.AppendLine();
        foreach (var target in Targets)
        {
            builder.AppendLine($"- [{target.Title}]({target.Url}) - Type: {target.Type}");
        }
        return builder.ToString();
    }
}

public sealed class StudyPlanOutput
{
    [Description("Short rationale explaining why these recommendations fit the student goals.")]
    public string Rationale { get; init; } = string.Empty;
    [Description("Name of the plan, usually aligned to a target exam/certification.")]
    public string PlanTitle { get; init; } = string.Empty;

    [Description("Total expected duration in weeks.")]
    public int DurationWeeks { get; init; }

    [Description("Expected weekly study hours.")]
    public int WeeklyHours { get; init; }

    [Description("Weekly milestones with clear outcomes and deliverables.")]
    public List<StudyWeekMilestone> WeeklyMilestones { get; init; } = [];
    [Description("Brief message to the user summarizing the study plan and encouraging commitment.")]
    public string BriefMessageToUser { get; set; } = string.Empty;

    public string AsMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"**Plan Title:** {PlanTitle}");
        builder.AppendLine($"**Rationale:** {Rationale}");
        builder.AppendLine($"**Duration:** {DurationWeeks} weeks");
        builder.AppendLine($"**Weekly Study Hours:** {WeeklyHours}");
        builder.AppendLine("### Weekly Milestones:");
        builder.AppendLine();
        foreach (var milestone in WeeklyMilestones)
        {
            builder.AppendLine($"#### Week {milestone.WeekNumber}: {milestone.Objective}");
            builder.AppendLine($"**Checkpoint:** {milestone.Checkpoint}");
            builder.AppendLine("**Daily Sessions:**");
            foreach (var session in milestone.Sessions)
            {
                builder.AppendLine($"- **{session.Day}**: {session.DurationMinutes} minutes. Tasks: {string.Join(", ", session.Tasks)}");
                builder.AppendLine("  URLs:");
                foreach (var url in session.Urls)
                {
                    builder.AppendLine($"  - {url}");
                }
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }
    public StudyPlanEntity ToStudyPlanEntity(string userId)
    {
        return new StudyPlanEntity(userId, this);
    }
}

public class StudyPlanEntity(string userId, StudyPlanOutput studyPlan)
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = userId;
    public StudyPlanOutput StudyPlan { get; set; } = studyPlan;
}

public sealed class EngagementPlanOutput
{
    [Description("Short rationale explaining why this engagement plan fits the student goals.")]
    public string Rationale { get; init; } = string.Empty;
    [Description("Email address receiving reminders and motivational messages.")]
    public string RecipientEmail { get; init; } = string.Empty;

    [Description("Reminder schedule mapped to study milestones.")]
    public List<ReminderMessage> Reminders { get; init; } = [];

    [Description("Additional motivational messages to improve consistency.")]
    public List<MotivationMessage> MotivationMessages { get; init; } = [];
    [Description("Brief message to the user summarizing the engagement plan and encouraging adherence.")]
    public string? BriefMessageToUser { get; set; }

    public string AsMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"**Rationale:** {Rationale}");
        builder.AppendLine($"**Recipient Email:** {RecipientEmail}");
        builder.AppendLine();
        builder.AppendLine("### Reminders:");
        foreach (var reminder in Reminders)
        {
            builder.AppendLine($"- **Schedule:** {reminder.Schedule}, **Subject:** {reminder.Subject}, **Body:** {reminder.Body}");
        }
        builder.AppendLine();
        builder.AppendLine("### Motivational Messages:");
        foreach (var message in MotivationMessages)
        {
            builder.AppendLine($"- **Schedule:** {message.Schedule}, **Message:** {message.Body}");
        }
        return builder.ToString();
    }
}

public sealed class LearningPathRecommendation
{
    [Description("Microsoft Learn item title.")]
    public string Title { get; init; } = string.Empty;

    [Description("Microsoft Learn URL for the item.")]
    public string Url { get; init; } = string.Empty;

    [Description("Estimated completion effort in hours.")]
    public int EstimatedHours { get; init; }

    [Description("Why this item is relevant for the student.")]
    public string Relevance { get; init; } = string.Empty;
}

public sealed class ModuleRecommendation
{
    [Description("Unique Module ID.")]
    public string Id { get; init; } = string.Empty;

    [Description("Module title.")]
    public string Title { get; init; } = string.Empty;

    [Description("Module URL on Microsoft Learn.")]
    public string Url { get; init; } = string.Empty;

    [Description("Skill area this module strengthens.")]
    public string SkillFocus { get; init; } = string.Empty;

    [Description("List of ALL unit IDs within the module (Required)")]
    public List<string> ModuleUnitIds { get; set; } = [];
    [JsonIgnore]
    public List<ModuleUnitRecommendation> ModuleUnits { get; set; } = [];
}

public sealed class ModuleUnitRecommendation
{
    [Description("Unique Unit ID.")] public string Id { get; init; } = string.Empty;
    [Description("Unit title.")] public string Title { get; init; } = string.Empty;

    [Description("Unit URL on Microsoft Learn.")]
    public string Url { get; init; } = string.Empty;
    public int DurationInMinutes { get; set; }
}

public sealed class CertificationOrExamRecommendation
{
    [Description("Exam, applied skill or certification title.")]
    public string Title { get; init; } = string.Empty;

    [Description("Type such as Certification or Exam.")]
    public string Type { get; init; } = string.Empty;

    [Description("Official URL for details.")]
    public string Url { get; init; } = string.Empty;
}

public sealed class StudyWeekMilestone
{
    [Description("Week number in the study plan timeline.")]
    public int WeekNumber { get; init; }

    [Description("Primary weekly objective.")]
    public string Objective { get; init; } = string.Empty;

    [Description("Daily study sessions for the week.")]
    public List<DailyStudySession> Sessions { get; init; } = [];

    [Description("Checkpoint used to validate weekly progress.")]
    public string Checkpoint { get; init; } = string.Empty;
}

public sealed class DailyStudySession
{
    [Description("Day label such as Monday or Day 1.")]
    public string Day { get; init; } = string.Empty;

    [Description("Planned duration in minutes.")]
    public int DurationMinutes { get; init; }

    [Description("Specific tasks for this session.")]
    public List<string> Tasks { get; init; } = [];
    [Description("Urls to the module units")]
    public List<string> Urls { get; init; } = [];
}

public sealed class ReminderMessage
{
    [Description("When to send the reminder in the schedule.")]
    public DateTime Schedule { get; init; }

    [Description("Reminder subject line.")]
    public string Subject { get; init; } = string.Empty;

    [Description("Reminder message body.")]
    public string Body { get; init; } = string.Empty;
    public ReminderEntity ToReminderEntity(string userId)
    {
        return new ReminderEntity
        {
            UserId = userId,
            RecipientEmail = userId,
            ScheduleUtc = Schedule,
            Subject = Subject,
            Body = Body,
            Status = "Pending",
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };
    }
}

public sealed class MotivationMessage
{
    [Description("Suggested send time for the motivational note.")]
    public DateTime Schedule { get; init; }

    [Description("Motivational message content.")]
    public string Body { get; init; } = string.Empty;
    public ReminderEntity ToReminderEntity(string userId)
    {
        return new ReminderEntity
        {
            UserId = userId,
            RecipientEmail = userId,
            ScheduleUtc = Schedule,
            Subject = "Motivation: " + Body[..Math.Min(50, Body.Length)] + "...",
            Body = Body,
            Status = "Pending",
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };
    }
}