using AgentsLeagueReasoningAgents.Models;
using Microsoft.AspNetCore.Components;

namespace AgentsLeagueReasoningAgents.Demo.Components.StudyHelpDisplay;

public partial class LearningPath
{
    [Parameter] public LearningPathCurationOutput? Output { get; set; }
}