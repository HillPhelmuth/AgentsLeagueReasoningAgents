using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MSLearnPlatformClient.Abstractions;
using System.Text.Json;
using MSLearnPlatformClient.Models.Catalog;

namespace AgentsLeagueReasoningAgents.Workflows;

public interface IPreparationWorkflowService
{
    Task<PreparationWorkflowResult> RunPreparationWithWorkflowAsync(PreparationWorkflowRequest request, CancellationToken cancellationToken = default);
    event Action<string, object>? AgentResponseEmitted;
    Task<PreparationWorkflowResult> RunPreparationAsync(PreparationWorkflowRequest request, CancellationToken cancellationToken = default);
    Task<PreparationWorkflowResult?> RunSingleAgentPreparationAsync(PreparationWorkflowRequest request, CancellationToken cancellationToken = default);
    event Action<string, string, object>? AgentToolInvoked;
}

public sealed class PreparationWorkflowService(
    IPreparationAgentFactory agentFactory,
    ILoggerFactory loggerFactory,
    IPreparationAssessmentStateStore stateStore, ILearnCatalogClient learnCatalogClient,
    ReminderRepository reminderRepository,
    ReminderService reminderService) : IPreparationWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private ILogger<PreparationWorkflowService> _logger = loggerFactory.CreateLogger<PreparationWorkflowService>();
    public event Action<string, object>? AgentResponseEmitted;
    public event Action<string, string, object>? AgentToolInvoked;

    private static string _seedOutputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos", "AgentsLeagueReasoningAgents", "AgentsLeagueReasoningAgents.Evals", "Datasets", "SeedSessions");
    // This method demonstrates running the preparation workflow in a more traditional way, invoking each agent sequentially and
    // passing outputs between them. The RunPreparationWithWorkflowAsync method below shows how to run the same workflow using
    // the AgentWorkflowBuilder and InProcessExecution for better orchestration and potential parallelism.
    public async Task<PreparationWorkflowResult> RunPreparationAsync(PreparationWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        agentFactory.AgentInvokedTool += HandleAgentInvokedTool;
        var curator = await agentFactory.CreateLearningPathCuratorAgentAsync(cancellationToken).ConfigureAwait(false);

        var planner = await agentFactory.CreateStudyPlanGeneratorAgentAsync(cancellationToken).ConfigureAwait(false);

        var engagement = await agentFactory.CreateEngagementAgentAsync(cancellationToken).ConfigureAwait(false);

        var curationPrompt = $"""

                                       Learning topics: {request.Topics}
                                       Weekly study hours: {request.WeeklyHours}
                                       Duration in weeks: {request.DurationWeeks}

                                       Produce JSON only matching the provided schema
                                       
                                       """;
        var curatedPromptOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<LearningPathCurationOutput>()
            }
        };
        var curatorSession = await curator.CreateSessionAsync();
        var curatedLearningPath = (await curator.RunAsync(curationPrompt, curatorSession, options: curatedPromptOptions, cancellationToken: cancellationToken).ConfigureAwait(false)).Text ?? string.Empty;

        var curatorserialized = await curator.SerializeSessionAsync(curatorSession);
#if DEBUG
        var outputPath = Path.Combine(_seedOutputDir, $"{curator.Name}", $"session_{Guid.NewGuid()}.json");

#endif
        var curatedStructured = DeserializeOrDefault<LearningPathCurationOutput>(curatedLearningPath);
        if (curatedStructured?.Modules != null)
        {
            List<string> moduleUnitsRequested = [];
            foreach (var module in curatedStructured.Modules)
            {
                var moduleModuleUnitIds = module.ModuleUnitIds;
                // Remove duplicates and already requested unit ids
                moduleModuleUnitIds = moduleModuleUnitIds.Except(moduleUnitsRequested).ToList();

                moduleUnitsRequested.AddRange(moduleModuleUnitIds);
                var unitRecords = await learnCatalogClient.GetCatalogItemsAsync(CatalogItemType.Unit, moduleModuleUnitIds, cancellationToken);
                module.ModuleUnits = unitRecords.Units.Select(unit => new ModuleUnitRecommendation()
                {
                    Id = unit.Uid ?? "",
                    Title = unit.Title ?? "",
                    Url = unit.Url ?? "",
                    DurationInMinutes = unit.DurationInMinutes.GetValueOrDefault()
                }).ToList();
            }
        }
        var curatedJson = SerializeFromObject(curatedStructured);
        string SerializeFromObject(object obj)
        {
            return JsonSerializer.Serialize(obj, JsonOptions);
        }
        AgentResponseEmitted?.Invoke(curator.Name, curatedStructured);
        var planPrompt = $"""

                           Learning topics: {request.Topics}
                           Weekly study hours: {request.WeeklyHours}
                           Duration in weeks: {request.DurationWeeks}

                           Curated resources:
                           {curatedJson}

                           Produce JSON only matching the provided schema
                           """;
        var planPromptOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<StudyPlanOutput>()
            }
        };
        var plannerSession = await planner.CreateSessionAsync();
        var studyPlan = (await planner.RunAsync(planPrompt, plannerSession, options: planPromptOptions, cancellationToken: cancellationToken)).Text;
        var plannerSerialized = await planner.SerializeSessionAsync(plannerSession);
#if DEBUG
        //outputPath = Path.Combine(SeedOutputDir, $"{planner.Name}", $"session_{Guid.NewGuid()}.json");
        //Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        //await File.WriteAllTextAsync(Path.Combine(outputPath), JsonSerializer.Serialize(plannerSerialized), cancellationToken);
#endif
        var studyPlanStructured = DeserializeOrDefault<StudyPlanOutput>(studyPlan);
        AgentResponseEmitted?.Invoke(planner.Name, studyPlanStructured);
        var engagementPrompt = $"""

                             User email: {request.StudentEmail}
                             Today's date: {DateTime.Now:d}
                             Study plan:
                             {studyPlan}

                             Produce JSON only matching the provided schema
                             """;
        var engagementPromptOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<EngagementPlanOutput>()
            }
        };
        var engageSession = await engagement.CreateSessionAsync();
        var engagementPlan = (await engagement.RunAsync(engagementPrompt, engageSession, options: engagementPromptOptions, cancellationToken: cancellationToken).ConfigureAwait(false)).Text;
        var engagementSerialized = await engagement.SerializeSessionAsync(engageSession);
#if DEBUG
        //outputPath = Path.Combine(SeedOutputDir, $"{engagement.Name}", $"session_{Guid.NewGuid()}.json");
        //Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        //await File.WriteAllTextAsync(Path.Combine(outputPath), JsonSerializer.Serialize(engagementSerialized), cancellationToken);
#endif
        var engagementStructured = DeserializeOrDefault<EngagementPlanOutput>(engagementPlan);
        AgentResponseEmitted?.Invoke(engagement.Name, engagementStructured);

        await PersistAndScheduleEngagementAsync(request.StudentEmail, engagementStructured, cancellationToken).ConfigureAwait(false);

        var workflowTranscript = $"""
                                  ## Workflow Transcript

                                  ### Learning Path Curation
                                  {curatedStructured.AsMarkdown()}

                                  ### Study Plan
                                  {studyPlanStructured.AsMarkdown()}

                                  ### Engagement Plan
                                  {engagementStructured.AsMarkdown()}
                                  """;

        var result = new PreparationWorkflowResult(curatedLearningPathStructured: curatedStructured,
            studyPlanStructured: studyPlanStructured,
            engagementPlanStructured: engagementStructured,
            workflowTranscript: workflowTranscript)
        {
            StudentEmail = request.StudentEmail,
            PreparationCompletedAtUtc = DateTimeOffset.UtcNow
        };

        await stateStore.SavePreparationStateAsync(request.StudentEmail, result, cancellationToken).ConfigureAwait(false);
        agentFactory.AgentInvokedTool -= HandleAgentInvokedTool;
        return result;
    }

    private void HandleAgentInvokedTool(string agent, string functionName, object parameters)
    {
        AgentToolInvoked?.Invoke(agent, functionName, parameters);
    }

    public async Task<PreparationWorkflowResult?> RunSingleAgentPreparationAsync(PreparationWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        agentFactory.AgentInvokedTool += HandleAgentInvokedTool;
        var agent = await agentFactory.CreateFullWorkflowAgentAsync(cancellationToken);
        var input = $"""

             Learning topics: {request.Topics}
             Weekly study hours: {request.WeeklyHours}
             Duration in weeks: {request.DurationWeeks}
             User email: {request.StudentEmail}
             Today's date: {DateTime.Now:d}
             Produce JSON only matching the provided schema

             """;
        var session = await agent.CreateSessionAsync(cancellationToken);
        var response = await agent.RunAsync(input, session, cancellationToken:cancellationToken);
        var sessionSerialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);


#if DEBUG
        var outputPath = Path.Combine(_seedOutputDir, $"{agent.Name}", $"session_{Guid.NewGuid()}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(sessionSerialized));
#endif
        var parsedResponse = JsonSerializer.Deserialize<PreparationWorkflowResult>(response.Text, new JsonSerializerOptions(){PropertyNameCaseInsensitive = true});
        if (parsedResponse is not null)
        {
            AgentResponseEmitted?.Invoke(agent.Name, parsedResponse);
            await PersistAndScheduleEngagementAsync(request.StudentEmail, parsedResponse.EngagementPlanStructured, cancellationToken).ConfigureAwait(false);
            await stateStore.SavePreparationStateAsync(request.StudentEmail, parsedResponse, cancellationToken).ConfigureAwait(false);
        }

        agentFactory.AgentInvokedTool -= HandleAgentInvokedTool;
        return parsedResponse;

    }

    private static T? DeserializeOrDefault<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }
    public async Task<PreparationWorkflowResult> RunPreparationWithWorkflowAsync(PreparationWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var curator = await agentFactory.CreateLearningPathCuratorAgentAsync(cancellationToken).ConfigureAwait(false);
        var planner = await agentFactory.CreateStudyPlanGeneratorAgentAsync(cancellationToken).ConfigureAwait(false);
        var engagement = await agentFactory.CreateEngagementAgentAsync(cancellationToken).ConfigureAwait(false);
        var workflow = AgentWorkflowBuilder.BuildSequential(curator, planner, engagement);

        await using var run = await InProcessExecution.StreamAsync(workflow, request.AsMarkdown(), cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        LearningPathCurationOutput? curatedLearningPathStructured = null;
        StudyPlanOutput? studyPlanStructured = null;
        EngagementPlanOutput? engagementPlanStructured = null;
        await foreach (var evt in run.WatchStreamAsync(cancellationToken))
        {
            _logger.LogInformation($"Received event: {evt}");
            if (evt is AgentResponseEvent agentResponse)
            {
                if (agentResponse.Response.TryDeserialize<LearningPathCurationOutput>(out var curatedOutput))
                {
                    AgentResponseEmitted?.Invoke(agentResponse.Response.AgentId!, curatedOutput);
                    curatedLearningPathStructured = curatedOutput;
                }
                else if (agentResponse.Response.TryDeserialize<StudyPlanOutput>(out var studyPlanOutput))
                {
                    AgentResponseEmitted?.Invoke(agentResponse.Response.AgentId!, studyPlanOutput);
                    studyPlanStructured = studyPlanOutput;
                }
                else if (agentResponse.Response.TryDeserialize<EngagementPlanOutput>(out var engagementPlanOutput))
                {
                    AgentResponseEmitted?.Invoke(agentResponse.Response.AgentId!, engagementPlanOutput);
                    engagementPlanStructured = engagementPlanOutput;
                }
            }

        }

        // Post-process the workflow result to extract structured outputs and build a transcript if needed.
        // This is a placeholder for demonstration; actual implementation would depend on how the workflow returns results.
        var result = new PreparationWorkflowResult(curatedLearningPathStructured: curatedLearningPathStructured,
        studyPlanStructured: studyPlanStructured,
        engagementPlanStructured: engagementPlanStructured,
        workflowTranscript: "Workflow execution completed. Transcript generation not implemented.")
        {
            StudentEmail = request.StudentEmail,
            PreparationCompletedAtUtc = DateTimeOffset.UtcNow
        };

        await PersistAndScheduleEngagementAsync(request.StudentEmail, engagementPlanStructured, cancellationToken).ConfigureAwait(false);

        await stateStore.SavePreparationStateAsync(request.StudentEmail, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task PersistAndScheduleEngagementAsync(
        string studentEmail,
        EngagementPlanOutput? engagementPlan,
        CancellationToken cancellationToken)
    {
        if (engagementPlan is null)
        {
            return;
        }

        var reminders = new List<ReminderEntity>();
        reminders.AddRange(engagementPlan.Reminders.Select(reminder => reminder.ToReminderEntity(studentEmail)));
        reminders.AddRange(engagementPlan.MotivationMessages.Select(message => message.ToReminderEntity(studentEmail)));

        foreach (var reminder in reminders)
        {
            reminder.ScheduleUtc = NormalizeToUtc(reminder.ScheduleUtc);

            await reminderRepository.UpsertReminderAsync(reminder, cancellationToken).ConfigureAwait(false);

            var sequenceNumber = await reminderService
                .ScheduleReminderAsync(
                    reminder.Id,
                    reminder.UserId,
                    new DateTimeOffset(reminder.ScheduleUtc, TimeSpan.Zero),
                    cancellationToken)
                .ConfigureAwait(false);

            await reminderRepository.MarkScheduledAsync(reminder.UserId, reminder.Id, sequenceNumber, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Scheduled reminder {ReminderId} for {RecipientEmail} at {ScheduleUtc}",
                reminder.Id,
                reminder.RecipientEmail,
                reminder.ScheduleUtc);
        }
    }

    private static DateTime NormalizeToUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }
}