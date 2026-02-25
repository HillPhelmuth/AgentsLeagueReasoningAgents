using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Options;
using AgentsLeagueReasoningAgents.Tools;
using AgentsLeagueReasoningAgents.Tools.Optional;
using AgentsLeagueReasoningAgents.Tools.Required;
using AgentsLeagueReasoningAgents.Workflows;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using static AgentsLeagueReasoningAgents.Agents.Prompts;

namespace AgentsLeagueReasoningAgents.Agents;

public interface IPreparationAgentFactory
{
    Task<AIAgent> CreateLearningPathCuratorAgentAsync(CancellationToken cancellationToken = default);

    Task<AIAgent> CreateStudyPlanGeneratorAgentAsync(CancellationToken cancellationToken = default);

    Task<AIAgent> CreateEngagementAgentAsync(CancellationToken cancellationToken = default);

    Task<AIAgent> CreateReadinessAssessmentAgentAsync(CancellationToken cancellationToken = default);
    Task<AIAgent> CreateFullWorkflowAgentAsync(CancellationToken cancellationToken = default);
    event Action<string, string, object>? AgentInvokedTool;
}

public sealed class PreparationAgentFactory(
    IOptions<AzureOpenAIOptions> options,
    LearnCatalogToolset learnCatalogToolset,
    MicrosoftLearnMcpToolset microsoftLearnMcpToolset,
    GitHubPracticeQuestionsToolset gitHubPracticeQuestionsToolset,
    GitHubStudyNotesToolset gitHubStudyNotesToolset,
    GitHubExamSyllabiToolset gitHubExamSyllabiToolset,
    GitHubAnkiDeckToolset gitHubAnkiDeckToolset,
    YouTubeStudyContentToolset youTubeStudyContentToolset,
    StackExchangeToolset stackExchangeToolset,
    PodcastFeedToolset podcastFeedToolset,
    GitHubCommunityHubToolset gitHubCommunityHubToolset,
    ExamTopicsToolset examTopicsToolset,
    OfficialLabExercisesToolset officialLabExercisesToolset,
    ILogger<PreparationAgentFactory> logger,
    ILoggerFactory loggerFactory, IConfiguration configuration) : IPreparationAgentFactory
{
    public event Action<string, string, object>? AgentInvokedTool;
    public async Task<AIAgent> CreateLearningPathCuratorAgentAsync(CancellationToken cancellationToken = default)
    {
        var learningPathTools = (await learnCatalogToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
            .Concat(await microsoftLearnMcpToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
            .ToArray();
        if (configuration["UseOptionalTools"] == "true")
        {
            learningPathTools = learningPathTools
                .Concat(await gitHubStudyNotesToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await gitHubExamSyllabiToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await youTubeStudyContentToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await podcastFeedToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await gitHubCommunityHubToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await examTopicsToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await officialLabExercisesToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .ToArray();
        }
        return CreateBaseAgent<LearningPathCurationOutput>(
            name: "learning-path-curator",
            instructions: CuratorInstructions,
            tools: learningPathTools);
    }

    public async Task<AIAgent> CreateStudyPlanGeneratorAgentAsync(CancellationToken cancellationToken = default)
    {
       var studyPlanTools = (await learnCatalogToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
            .Concat(await microsoftLearnMcpToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
            .ToArray();
        if (configuration["UseOptionalTools"] == "true")
        {
            studyPlanTools = studyPlanTools
                .Concat(await gitHubStudyNotesToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await gitHubExamSyllabiToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await gitHubCommunityHubToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await officialLabExercisesToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .ToArray();
        }
        return CreateBaseAgent<StudyPlanOutput>(
            name: "study-plan-generator",
            instructions: StudyPlannerInstructions,
            tools: studyPlanTools);
    }

    public async Task<AIAgent> CreateFullWorkflowAgentAsync(CancellationToken cancellationToken = default)
    {
        var workflowTools = (await learnCatalogToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
            .Concat(await microsoftLearnMcpToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false));
        if (configuration["UseOptionalTools"] == "true")
        {
            workflowTools = workflowTools
                .Concat(await gitHubStudyNotesToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await gitHubExamSyllabiToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await youTubeStudyContentToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await podcastFeedToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await gitHubCommunityHubToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await examTopicsToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await officialLabExercisesToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .ToArray();
        }
        return CreateBaseAgent<PreparationWorkflowResult>("full-workflow-agent", FullWorkflowAgentInstructions, workflowTools.ToArray());
    }

    public Task<AIAgent> CreateEngagementAgentAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateBaseAgent<EngagementPlanOutput>(
            name: "engagement-agent",
            instructions: EngagementInstructions,
            tools: []));
    }

    public Task<AIAgent> CreateReadinessAssessmentAgentAsync(CancellationToken cancellationToken = default)
    {
        return CreateReadinessAssessmentAgentCoreAsync(cancellationToken);
    }

    private async Task<AIAgent> CreateReadinessAssessmentAgentCoreAsync(CancellationToken cancellationToken)
    {
        var tools = (await microsoftLearnMcpToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
            .ToArray();
        if (configuration["UseOptionalTools"] == "true")
        {
            tools = tools
                .Concat(await gitHubPracticeQuestionsToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await gitHubAnkiDeckToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await stackExchangeToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .Concat(await youTubeStudyContentToolset.GetToolsAsync(cancellationToken).ConfigureAwait(false))
                .ToArray();
        }
        return CreateBaseAgent<AssessmentQuestionSetOutput>(
            name: "readiness-assessment-agent",
            instructions: ReadinessAssessmentInstructions,
            tools: tools);
    }

    private AIAgent CreateBaseAgent<TOutput>(string name, string instructions, IReadOnlyList<AITool> tools)
    {
        var endpoint = options.Value.Endpoint;
        var deployment = options.Value.DeploymentName;
        var apiKey = options.Value.ApiKey;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Azure OpenAI settings are required. Configure AzureOpenAI:Endpoint, AzureOpenAI:DeploymentName, and AzureOpenAI:ApiKey.");
        }

        IChatClient? chatClient;
        if (!options.Value.UseOnlyOpenAI)
            chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
            .GetChatClient(deployment).AsIChatClient();
        else
        {
            chatClient = new OpenAIClient(configuration["OpenAI:ApiKey"]).GetChatClient("gpt-4.1-mini").AsIChatClient();
        }

        var wrappedClient = chatClient;

        var agent = wrappedClient.AsAIAgent(
            new ChatClientAgentOptions()
            {
                Name = name,
                ChatOptions = new ChatOptions()
                {
                    Instructions = instructions,
                    Tools = tools.ToList(),
                    // use ResponseFormat to enforce structured output for better parsing and reliability
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<TOutput>()
                }
            }).AsBuilder().Use(LogToolCallsMiddlewareAsync).Build();
        return agent;
    }

    private async ValueTask<object?> LogToolCallsMiddlewareAsync(AIAgent agent, FunctionInvocationContext context, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Agent {AgentName} invoking tool {ToolName} with call id {CallId} and arguments {Arguments}",
            agent.Name,
            context.CallContent?.Name,
            context.CallContent?.CallId,
            context.CallContent?.Arguments);
        AgentInvokedTool?.Invoke(agent.Name ?? "agent not known", context.CallContent?.Name ?? "function not known", context.CallContent?.Arguments ?? (object)"no args");
        var result = await next(context, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Agent {AgentName} tool {ToolName} call {CallId} completed with result {Result}",
            agent.Name,
            context.CallContent?.Name,
            context.CallContent?.CallId,
            result);

        return result;
    }
}