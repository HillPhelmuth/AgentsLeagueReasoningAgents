using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.DependencyInjection;
using AgentsLeagueReasoningAgents.Evals;
using AgentsLeagueReasoningAgents.Workflows;
using Azure;
using Azure.AI.OpenAI;
using Azure.Messaging.ServiceBus;
using HillPhelmuth.SemanticKernel.LlmAsJudgeEvals;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OpenAI;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

var options = RunnerOptions.Parse(args);
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: false).AddJsonFile("appsettings.json", optional: false)
    .Build();

var serviceCollection = new ServiceCollection();
var clientOptions = new CosmosClientOptions()
{
    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    }
};
var cosmosClient = new CosmosClient(configuration["ConnectionStrings:ReminderDb"], clientOptions);
serviceCollection.AddSingleton(cosmosClient);
var serviceBusClient = new ServiceBusClient(configuration["ConnectionStrings:ServiceBus"]);
serviceCollection.AddSingleton(serviceBusClient);
serviceCollection.AddReasoningAgentsPreparation(configuration);
serviceCollection.AddSingleton(configuration);
var services = serviceCollection.BuildServiceProvider();
var runnerConfig = RunnerConfig.FromConfig(configuration);
//using var evalScope = services.CreateScope();

if (!runnerConfig.IsValid)
{
    Console.Error.WriteLine("Missing Azure OpenAI configuration. Set one of these env var sets:");
    Console.Error.WriteLine("- AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, AZURE_OPENAI_DEPLOYMENT");
    Console.Error.WriteLine("- AzureOpenAI__Endpoint, AzureOpenAI__ApiKey, AzureOpenAI__DeploymentName");
    return 2;
}

var datasetRoot = Path.GetFullPath(options.DatasetRoot);
//if (!Directory.Exists(datasetRoot))
//{
//    Console.Error.WriteLine($"Dataset root not found: {datasetRoot}");
//    return 2;
//}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};
var agentFactory = services.GetRequiredService<IPreparationAgentFactory>();
var preparationWorkflowService = services.GetRequiredService<IPreparationWorkflowService>();
var chatClient = new AzureOpenAIClient(new Uri(runnerConfig.Endpoint!), new AzureKeyCredential(runnerConfig.ApiKey!));
var openAIClient = new OpenAIClient(configuration["OpenAI:ApiKey"]);
//.GetChatClient("gpt-4.1-mini")
//.AsIChatClient();
var kernel = Kernel.CreateBuilder().AddOpenAIChatCompletion("gpt-4.1-nano", openAIClient)/*.AddAzureOpenAIChatCompletion("gpt-4.1-nano", chatClient)*/.Build();
var evalService = new EvalService(kernel);
var runner = new DatasetEvalRunner(evalService, options, datasetRoot, agentFactory, preparationWorkflowService);

var report = await runner.RunAsync(cts.Token).ConfigureAwait(false);

var outputPath = options.OutputPath;
if (string.IsNullOrWhiteSpace(outputPath))
{
    var reportsDir = Path.Combine(datasetRoot, "reports");
    Directory.CreateDirectory(reportsDir);
    outputPath = Path.Combine(reportsDir, $"eval-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
}

var reportJson = JsonSerializer.Serialize(report, JsonDefaults.WriteIndented);
await File.WriteAllTextAsync(outputPath, reportJson, cts.Token).ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("=== Per-Agent Evaluation Report ===");
foreach (var item in report.PerAgent.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"Agent: {item.Key}");
    Console.WriteLine($"  Cases: {item.Value.TotalCases}");
    Console.WriteLine($"  Composite Avg: {item.Value.CompositeAverage.ToString("F3", CultureInfo.InvariantCulture)}");
    Console.WriteLine($"  Pass Rate: {(item.Value.PassRate * 100).ToString("F2", CultureInfo.InvariantCulture)}%");

    foreach (var metric in item.Value.MetricAverages.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  - {metric.Key}: score={metric.Value.AverageScore.ToString("F3", CultureInfo.InvariantCulture)}, prob={metric.Value.AverageProbScore.ToString("F3", CultureInfo.InvariantCulture)}");
    }

    Console.WriteLine();
}

Console.WriteLine($"Overall cases: {report.TotalCases}");
Console.WriteLine($"Overall pass rate: {(report.OverallPassRate * 100).ToString("F2", CultureInfo.InvariantCulture)}%");
Console.WriteLine($"Report written: {Path.GetFullPath(outputPath)}");

return 0;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions CaseInsensitive = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions WriteIndented = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}

public class FileHelper
{
    public static T ExtractFromAssembly<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var jsonName = assembly.GetManifestResourceNames()
            .SingleOrDefault(s => s.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)) ?? "";
        using var stream = assembly.GetManifestResourceStream(jsonName);
        using var reader = new StreamReader(stream);
        object result = reader.ReadToEnd();
        if (typeof(T) == typeof(string))
            return (T)result;
        return JsonSerializer.Deserialize<T>(result.ToString());
    }
}
internal sealed class RunnerOptions
{
    public string DatasetRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos", "AgentsLeagueReasoningAgents", "AgentsLeagueReasoningAgents.Evals", "Datasets");
    public string? OutputPath { get; set; }
    public int? MaxCasesPerAgent { get; set; } = 10;
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount - 1;
    public HashSet<string> AgentFilter { get; } = ["preparation-workflow-multi-agent", "preparation-workflow-single-agent"];
    public List<string> DatasetFiles { get; } = [@".\AgentsLeagueReasoningAgents.Evals\Datasets\full-workflow\preparation-workflow.comparative.explain.jsonl"];

    public static RunnerOptions Parse(string[] args)
    {
        var options = new RunnerOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--dataset-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.DatasetRoot = args[++i];
                continue;
            }

            if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.OutputPath = args[++i];
                continue;
            }

            if (arg.Equals("--max-cases-per-agent", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0)
                {
                    options.MaxCasesPerAgent = count;
                }

                continue;
            }

            if (arg.Equals("--max-concurrency", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxConcurrency) && maxConcurrency > 0)
                {
                    options.MaxConcurrency = maxConcurrency;
                }

                continue;
            }

            if (arg.Equals("--agent", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.AgentFilter.Add(args[++i]);
                continue;
            }

            if (arg.Equals("--dataset-file", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.DatasetFiles.Add(args[++i]);
            }
        }

        return options;
    }
}

internal sealed class RunnerConfig
{
    public string? Endpoint { get; init; }
    public string? ApiKey { get; init; }
    public string? Deployment { get; init; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Deployment);

    public static RunnerConfig FromEnvironment()
    {
        return new RunnerConfig
        {
            Endpoint =
                Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
                Environment.GetEnvironmentVariable("AzureOpenAI__Endpoint"),
            ApiKey =
                Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ??
                Environment.GetEnvironmentVariable("AzureOpenAI__ApiKey"),
            Deployment =
                Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ??
                Environment.GetEnvironmentVariable("AzureOpenAI__DeploymentName")
        };
    }
    public static RunnerConfig FromConfig(IConfiguration config)
    {
        return new RunnerConfig
        {
            Endpoint = config["AzureOpenAI:Endpoint"],
            ApiKey = config["AzureOpenAI:ApiKey"],
            Deployment = config["AzureOpenAI:DeploymentName"]
        };
    }
}

internal sealed class EvalRunReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public int TotalCases { get; init; }
    public int TotalPassed { get; init; }
    public double OverallPassRate { get; init; }
    public Dictionary<string, AgentEvaluationReport> PerAgent { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<CaseEvaluationResult> Cases { get; init; } = [];
}

internal sealed class AgentEvaluationReport
{
    public int TotalCases { get; init; }
    public int PassedCases { get; init; }
    public double PassRate { get; init; }
    public double CompositeAverage { get; init; }
    public string Explanation { get; set; } = string.Empty;
    public Dictionary<string, MetricAverageReport> MetricAverages { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class MetricAverageReport
{
    public double AverageScore { get; init; }
    public double AverageProbScore { get; init; }
}

internal sealed class CaseEvaluationResult
{
    public string CaseId { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string ScenarioId { get; init; } = string.Empty;
    public string ThresholdProfile { get; init; } = string.Empty;
    public double CompositeScore { get; init; }
    public bool Passed { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<MetricEvaluationResult> Metrics { get; init; } = [];
}

internal sealed class MetricEvaluationResult
{
    public string MetricName { get; init; } = string.Empty;
    public string EvalName { get; init; } = string.Empty;
    public double Score { get; init; }
    public double ProbScore { get; init; }
    public string? Reasoning { get; init; }
    public string? ChainOfThought { get; init; }
}
