using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace AgentsLeagueReasoningAgents.Tools;

public sealed class MicrosoftLearnMcpToolset(
    IOptions<MicrosoftLearnMcpOptions> options,
    ILogger<MicrosoftLearnMcpToolset> logger) : IAIToolset
{
    private McpClient? _mcpClient;
    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        _mcpClient ??= await McpClient.CreateAsync(new HttpClientTransport(new()
        {
            Endpoint = new Uri(options.Value.Endpoint),
            Name = options.Value.Name
        }), cancellationToken: cancellationToken).ConfigureAwait(false);
        var mcpTools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Loaded {Count} tools from Microsoft Learn MCP endpoint {Endpoint}", mcpTools.Count, options.Value.Endpoint);

        return mcpTools.ToList();
    }
}