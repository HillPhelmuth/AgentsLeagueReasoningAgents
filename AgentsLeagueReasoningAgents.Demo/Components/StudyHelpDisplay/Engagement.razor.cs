using AgentsLeagueReasoningAgents.Models;
using Microsoft.AspNetCore.Components;

namespace AgentsLeagueReasoningAgents.Demo.Components.StudyHelpDisplay;

public partial class Engagement
{
    [Parameter] public EngagementPlanOutput? Output { get; set; }

    private IReadOnlyList<CalendarDay> CalendarDays { get; set; } = [];

    protected override void OnParametersSet()
    {
        CalendarDays = BuildCalendarDays(Output);
    }

    private static IReadOnlyList<CalendarDay> BuildCalendarDays(EngagementPlanOutput? output)
    {
        if (output is null)
        {
            return [];
        }

        var entries = new List<CalendarEntry>();

        entries.AddRange(output.Reminders.Select(reminder => new CalendarEntry(
            reminder.Schedule,
            "Reminder",
            reminder.Subject,
            reminder.Body)));

        entries.AddRange(output.MotivationMessages.Select(message => new CalendarEntry(
            message.Schedule,
            "Motivation",
            null,
            message.Body)));

        return entries
            .OrderBy(e => e.Schedule)
            .GroupBy(e => e.Schedule.Date)
            .Select(group => new CalendarDay(
                group.Key,
                group.OrderBy(e => e.Schedule)
                    .Select(e => new CalendarEntryView(
                        e.Schedule.ToString("HH:mm"),
                        e.Kind,
                        e.Subject,
                        e.Body))
                    .ToList()))
            .ToList();
    }

    private sealed record CalendarEntry(DateTime Schedule, string Kind, string? Subject, string Body);

    private sealed record CalendarEntryView(string TimeLabel, string Kind, string? Subject, string Body);

    private sealed record CalendarDay(DateTime Date, IReadOnlyList<CalendarEntryView> Entries);
}