using Microsoft.Extensions.AI;

namespace AgentsLeagueReasoningAgents.Agents;

public interface IAIToolset
{
    Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default);
}