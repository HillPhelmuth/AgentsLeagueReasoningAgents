namespace AgentsLeagueReasoningAgents.EmailFunction.Models;

public sealed record ReminderDispatchMessage(
    string ReminderId,
    string UserId
);
