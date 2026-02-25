using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Options;
using AgentsLeagueReasoningAgents.Services;
using AgentsLeagueReasoningAgents.Tools;
using AgentsLeagueReasoningAgents.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using AgentsLeagueReasoningAgents.Tools.Optional;
using AgentsLeagueReasoningAgents.Tools.Required;
using MSLearnPlatformClient.Options;
using MSLearnPlatformClient.Services;

namespace AgentsLeagueReasoningAgents.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReasoningAgentsPreparation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.Configure<AzureOpenAIOptions>(configuration.GetSection("AzureOpenAI"));
        services.Configure<MicrosoftLearnMcpOptions>(configuration.GetSection("MicrosoftLearnMcp"));
        services.Configure<GitHubOptions>(configuration.GetSection("GitHub"));
        services.Configure<DitectrevOptions>(configuration.GetSection("Ditectrev"));
        services.Configure<StudyNotesOptions>(configuration.GetSection("StudyNotes"));
        services.Configure<AnkiDeckOptions>(configuration.GetSection("AnkiDecks"));
        services.Configure<YouTubeOptions>(configuration.GetSection("YouTube"));
        services.Configure<StackExchangeOptions>(configuration.GetSection("StackExchange"));
        services.Configure<PodcastOptions>(configuration.GetSection("Podcasts"));
        services.Configure<OfficialLabsOptions>(configuration.GetSection("OfficialLabs"));

        services.AddMemoryCache();
        services.AddHttpClient("GitHub", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AgentsLeagueReasoningAgents/1.0");
        });
        services.AddHttpClient("YouTube", client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
        });
        services.AddHttpClient("StackExchange", client =>
        {
            client.BaseAddress = new Uri("https://api.stackexchange.com/2.3/");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });

        services.AddMsLearnCatalogClient(options =>
        {
            var section = configuration.GetSection("LearnCatalog");
            section.Bind(options);
            options.Scopes = section.GetSection(nameof(LearnCatalogOptions.Scopes)).Get<string[]>()
                             ?? options.Scopes;
            options.ClientId = configuration["AzureAD:ClientId"];
            options.ClientSecret = configuration["AzureAD:ClientSecret"];
            options.TenantId = configuration["AzureAD:TenantId"];
        });

        services.AddSingleton<LearnCatalogToolset>();
        services.AddSingleton<MicrosoftLearnMcpToolset>();
        services.AddSingleton<GitHubContentService>();
        services.AddSingleton<MarkdownParserService>();
        services.AddSingleton<DitectrevMarkdownParser>();
        services.AddSingleton<AnkiExtractorService>();
        services.AddSingleton<YouTubeService>();
        services.AddSingleton<StackExchangeService>();
        services.AddSingleton<PodcastFeedService>();

        services.AddSingleton<GitHubPracticeQuestionsToolset>();
        services.AddSingleton<GitHubStudyNotesToolset>();
        services.AddSingleton<GitHubExamSyllabiToolset>();
        services.AddSingleton<GitHubAnkiDeckToolset>();
        services.AddSingleton<YouTubeStudyContentToolset>();
        services.AddSingleton<StackExchangeToolset>();
        services.AddSingleton<PodcastFeedToolset>();
        services.AddSingleton<GitHubCommunityHubToolset>();
        services.AddSingleton<ExamTopicsToolset>();
        services.AddSingleton<OfficialLabExercisesToolset>();
        services.AddSingleton(sp =>
        {
            var serviceBusClient = sp.GetRequiredService<Azure.Messaging.ServiceBus.ServiceBusClient>();
            var queueName = configuration["Reminders:QueueName"] ?? "reminders";
            return new ReminderService(serviceBusClient, queueName);
        });
        services.AddSingleton<ReminderRepository>();

        services.AddScoped<IPreparationAssessmentStateStore, PreparationAssessmentStateStore>();
        services.AddSingleton<IPreparationAgentFactory, PreparationAgentFactory>();
        services.AddScoped<IPreparationWorkflowService, PreparationWorkflowService>();
        services.AddScoped<IAssessmentWorkflowService, AssessmentWorkflowService>();

        return services;
    }
}