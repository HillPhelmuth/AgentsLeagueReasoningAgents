using Microsoft.Identity.Client;

namespace AgentsLeagueReasoningAgents.Options;

public sealed class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
    public bool UseOnlyOpenAI { get; set; }
}