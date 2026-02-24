using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentsLeagueReasoningAgents.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using MSLearnPlatformClient.Abstractions;
using MSLearnPlatformClient.Models.Catalog;
using OpenAI;

namespace AgentsLeagueReasoningAgents.Tools;

public sealed class LearnCatalogToolset(ILearnCatalogClient learnCatalogClient, IConfiguration configuration) : IAIToolset
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private AIAgent CreateFilterAgent<TOutput>(params CatalogItemType[] itemTypes)
    {
        var client = new OpenAIClient(new ApiKeyCredential(configuration["OpenRouter:ApiKey"]), new OpenAIClientOptions() { Endpoint = new Uri("https://openrouter.ai/api/v1") }).GetChatClient("gpt-oss-120b:nitro");
        var instructions = $"""
                            Filter the following list of Microsoft Learn catalog items to those most relevant to the specified query. Only consider items of the following types: {string.Join(", ", itemTypes)}. 
                            You will be provided with a json array of catalog items, and a user query. Each catalog item will have a title, description, and associated metadata such as subjects and roles.
                            Return a JSON object containing the relevant items, ranked in order of relevance to the query. Never return empty results - if no items are relevant, return the top 3 most relevant items.
                            Ensure that you provide **all** details of the relevant items in the output, including title, description, url, subjects, roles, and ALL other metadata provided.
                             Ensure the output adheres to the specified JSON schema.
                            """;
        var agent = client.AsIChatClient().AsAIAgent(new ChatClientAgentOptions()
        {
            Name = "filter-agent",
            ChatOptions = new ChatOptions()
            {
                Instructions = instructions,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<TOutput>()
            }
        });
        return agent;
    }
    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(SearchLearningPathsAsync),
            AIFunctionFactory.Create(SearchModulesAsync),
            AIFunctionFactory.Create(SearchCertificationsAndExamsAsync),
                AIFunctionFactory.Create(GetModulesAsync)
        ];

        return Task.FromResult(tools);
    }

    [Description("Find Microsoft Learn learning paths relevant to one or more certification topics.")]
    public async Task<string> SearchLearningPathsAsync(
        [Description("MS learn certification subjects. include all that apply. If you're unsure, add it.")] LearnSubject[] subjects,
        [Description("Applicable job roles for the subject matter.")] Role[] jobRoles,
        [Description("A description of the items that can be used to query the results to provide only the most relevant results.")] string query,
        [Description("Maximum number of learning paths to return.")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var response = await learnCatalogClient.QueryCatalogAsync(new CatalogQuery
        {
            Type = [CatalogItemType.LearningPath, CatalogItemType.Course],
            Subjects = subjects,
            //Levels = difficultyLevels,
            Roles = jobRoles,
            MaxPageSize = Math.Clamp(maxResults, 90, 100)
        }, cancellationToken).ConfigureAwait(false);

        var searchResultJson = JsonSerializer.Serialize(response.LearningPaths.Take(maxResults), SerializerOptions);
        var filterAgent = CreateFilterAgent<CoursesAndLearnPathResponse>(CatalogItemType.LearningPath, CatalogItemType.Course);
        var input = $"""
                     ### User Query
                     {query}
                     
                     ### Catalog Items
                     {searchResultJson}
                     """;
        var filterResponse = await filterAgent.RunAsync(input, cancellationToken: cancellationToken);
        return filterResponse.Text;
    }
    [Description("Get the specific MS Learn modules extracted from previous `LearningPath` Searches.")]
    public async Task<string> GetModulesAsync([Description("The module IDs extracted from `modules` property")] string[] moduleIds, CancellationToken cancellationToken = default)
    {
        List<ModuleRecord> modules = [];
        foreach (var id in moduleIds)
        {
            var response = await learnCatalogClient.GetCatalogItemAsync(CatalogItemType.Module, id, cancellationToken).ConfigureAwait(false);
            if (response is not null)
            {
                modules.AddRange(response.Modules);
            }
        }
        //var response = await learnCatalogClient.QueryCatalogAsync(new CatalogQuery
        //{
        //    Type = [CatalogItemType.Module],
        //    Uid = moduleIds,
        //    MaxPageSize = Math.Clamp(moduleIds.Length, 90, 100)
        //}, cancellationToken).ConfigureAwait(false);

        var modulesResultsJson = JsonSerializer.Serialize(modules, SerializerOptions);
        return modulesResultsJson;
    }
    [Description("Find Microsoft Learn modules for subjects or job roles. Use ONLY when you have no specific modules to get from `GetModules`")]
    public async Task<string> SearchModulesAsync(
        [Description("MS learn certification subjects, parsed from input. Include *ALL* that may apply. If you're unsure, include it")] LearnSubject[] subjects,
        [Description("Applicable job roles for the subject matter.")] Role[] jobRoles,
        [Description("A description of the items that can be used to query the results to provide only the most relevant results.")] string query,
        [Description("Maximum number of modules to return.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
    {

        var response = await learnCatalogClient.QueryCatalogAsync(new CatalogQuery
        {
            Type = [CatalogItemType.Module],
            Subjects = subjects,
            //Levels = difficultyLevels,
            Roles = jobRoles,
            MaxPageSize = Math.Clamp(maxResults, 90, 100)
        }, cancellationToken).ConfigureAwait(false);
        var queryObjects = response.Modules.Select(x => new ModuleResponseItem(x.Uid, x.Title));
        var modulesResultsJson = JsonSerializer.Serialize(queryObjects, SerializerOptions);
        var filterAgent = CreateFilterAgent<ModuleFilterResponse>(CatalogItemType.Module);
        var input = $"""
                     ### User Query
                     {query}
                     
                     ### Catalog Items
                     {modulesResultsJson}
                     """;
        var filterResponse = await filterAgent.RunAsync(input, cancellationToken: cancellationToken);
        var filter = JsonSerializer.Deserialize<ModuleFilterResponse>(filterResponse.Text);
        var filtered = new ModuleResponse
        {
            Reasoning = filter.Reasoning,
            Modules = response.Modules.Where(m => filter.Modules.Any(fm => fm.Id == m.Uid)).ToList()
        };
        return JsonSerializer.Serialize(filtered, SerializerOptions);
    }
    
    [Description("Find relevant certifications and exams in Microsoft Learn catalog for a target area.")]
    public async Task<string> SearchCertificationsAndExamsAsync(
        [Description("MS learn certification subjects. Include *ALL* that may apply. If you're unsure, include it")] LearnSubject[] subjects,
        [Description("Applicable job roles for the subject matter.")] Role[] jobRoles,
        [Description("A description of user needs that can be used to query the results to provide only the most relevant results.")] string query,
        [Description("Maximum records for each category.")] int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var catalogQuery = new CatalogQuery
        {
            Type = [CatalogItemType.Certification, CatalogItemType.Exam],
            Subjects = subjects,
            Roles = jobRoles,
            MaxPageSize = Math.Clamp(maxResults, 90, 100)
        };

        var response = await learnCatalogClient.QueryCatalogAsync(catalogQuery, cancellationToken).ConfigureAwait(false);
        var filterAgent = CreateFilterAgent<ExamAndCertResponse>(CatalogItemType.Certification, CatalogItemType.Exam);
        var batchSize = Math.Clamp(maxResults, 1, 50);
        var certificationCount = response.Certifications.Count;
        var examCount = response.Exams.Count;
        var batchCount = Math.Max(
            (int)Math.Ceiling(certificationCount / (double)batchSize),
            (int)Math.Ceiling(examCount / (double)batchSize));

        JsonNode? mergedResults = null;
        List<string> fallbackResponses = [];

        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            var certificationBatch = response.Certifications
                .Skip(batchIndex * batchSize)
                .Take(batchSize)
                .ToList();
            var examBatch = response.Exams
                .Skip(batchIndex * batchSize)
                .Take(batchSize)
                .ToList();

            if (certificationBatch.Count == 0 && examBatch.Count == 0)
            {
                continue;
            }

            var payload = new
            {
                Certifications = certificationBatch,
                Exams = examBatch
            };
            var input = $"""
                         ### User Query
                         {query}
                         
                         ### Catalog Items
                         {JsonSerializer.Serialize(payload, SerializerOptions)}
                         """;
            var filterResponse = await filterAgent.RunAsync(input, cancellationToken: cancellationToken);

            if (!TryMergeBatchResponse(filterResponse.Text, ref mergedResults))
            {
                fallbackResponses.Add(filterResponse.Text);
            }
        }

        if (mergedResults is not null && fallbackResponses.Count == 0)
        {
            return mergedResults.ToJsonString(SerializerOptions);
        }

        if (mergedResults is not null)
        {
            fallbackResponses.Insert(0, mergedResults.ToJsonString(SerializerOptions));
        }

        return fallbackResponses.Count == 1
            ? fallbackResponses[0]
            : string.Join("\n\n", fallbackResponses);
    }

    private static bool TryMergeBatchResponse(string responseText, ref JsonNode? mergedResults)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(responseText);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null)
        {
            return false;
        }

        if (mergedResults is null)
        {
            mergedResults = parsed;
            return true;
        }

        if (mergedResults is JsonArray mergedArray && parsed is JsonArray parsedArray)
        {
            foreach (var item in parsedArray)
            {
                mergedArray.Add(item?.DeepClone());
            }

            return true;
        }

        if (mergedResults is JsonObject mergedObject && parsed is JsonObject parsedObject)
        {
            foreach (var property in parsedObject)
            {
                if (property.Value is JsonArray propertyArray)
                {
                    if (mergedObject[property.Key] is JsonArray existingArray)
                    {
                        foreach (var item in propertyArray)
                        {
                            existingArray.Add(item?.DeepClone());
                        }
                    }
                    else
                    {
                        mergedObject[property.Key] = propertyArray.DeepClone();
                    }

                    continue;
                }

                if (!mergedObject.ContainsKey(property.Key))
                {
                    mergedObject[property.Key] = property.Value?.DeepClone();
                }
            }

            return true;
        }

        return false;
    }
}