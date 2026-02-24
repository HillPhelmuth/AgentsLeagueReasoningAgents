using AgentsLeagueReasoningAgents.Models;
using Microsoft.AspNetCore.Components;

namespace AgentsLeagueReasoningAgents.Demo.Components.StudyHelpDisplay;

public partial class StudyPlan
{
    [Parameter] public StudyPlanOutput? Output { get; set; }
}