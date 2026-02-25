using AgentsLeagueReasoningAgents.Evals;
using AgentsLeagueReasoningAgents.Evals.CustomEvals;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Services;
using AgentsLeagueReasoningAgents.Workflows;
using Markdig;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components;
using MSLearnPlatformClient.Abstractions;
using MSLearnPlatformClient.Models.Catalog;
using MSLearnPlatformClient.Services;
using System.Text.Json;

namespace AgentsLeagueReasoningAgents.Demo.Components.Pages;

public partial class Home : IDisposable
{
    private sealed record ToolInvocationViewModel(
        string Agent,
        string ToolName,
        string ParametersJson,
        DateTimeOffset InvokedAtUtc);

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IPreparationAssessmentStateStore StateStore { get; set; } = default!;

    private string Topics { get; set; } = "Azure AI Engineer Associate";
    private string StudentEmail { get; set; } = "funnytrumpsmean@gmail.com";
    private int WeeklyHours { get; set; } = 6;
    private int DurationWeeks { get; set; } = 8;
    private bool UseSingleAgent { get; set; }
    private bool IsRunning { get; set; }
    private bool IsToolModalOpen { get; set; }
    private string? ErrorMessage { get; set; }
    private PreparationWorkflowResult? Result { get; set; }
    private List<ToolInvocationViewModel> ToolInvocations { get; } = [];
    [Inject] private ILearnCatalogClient LearnCatalogClient { get; set; } = default!;
    protected override Task OnInitializedAsync()
    {
        PreparationWorkflowService.AgentResponseEmitted += HandleAgentResponseEmitted;
        PreparationWorkflowService.AgentToolInvoked += HandleAgentToolInvoked;
        return base.OnInitializedAsync();
    }

    private string? tempString;
    

    public void Dispose()
    {
        PreparationWorkflowService.AgentResponseEmitted -= HandleAgentResponseEmitted;
        PreparationWorkflowService.AgentToolInvoked -= HandleAgentToolInvoked;
    }

    private void HandleAgentResponseEmitted(string agent, object responseContent)
    {
        Result ??= new PreparationWorkflowResult();
        switch (responseContent)
        {
            // Check type of responseContent and update Result accordingly
            case LearningPathCurationOutput learningPath:
                Result.CuratedLearningPathStructured = learningPath;
                break;
            case StudyPlanOutput studyPlan:
                Result.StudyPlanStructured = studyPlan;
                break;
            case EngagementPlanOutput engagementPlan:
                Result.EngagementPlanStructured = engagementPlan;
                break;
            case string transcript:
                Result.WorkflowTranscript = transcript;
                break;
            case PreparationWorkflowResult fullResult:
                Result = fullResult;
                break;
        }
        InvokeAsync(StateHasChanged);
    }

    private async Task RunWorkflowAsync()
    {
        IsRunning = true;
        ErrorMessage = null;
        Result = null;
        ToolInvocations.Clear();
        IsToolModalOpen = false;

        try
        {
            var request = new PreparationWorkflowRequest(Topics, StudentEmail, WeeklyHours, DurationWeeks);
            if (UseSingleAgent)
            {
                Result = await PreparationWorkflowService.RunSingleAgentPreparationAsync(request);
            }
            else
                Result = await PreparationWorkflowService.RunPreparationAsync(request);

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void HandleAgentToolInvoked(string agent, string toolName, object parameters)
    {
        var serializedParameters = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        ToolInvocations.Add(new ToolInvocationViewModel(
            agent,
            toolName,
            serializedParameters,
            DateTimeOffset.UtcNow));

        IsToolModalOpen = true;
        InvokeAsync(StateHasChanged);
    }

    private void OpenToolModal()
    {
        IsToolModalOpen = true;
    }

    private void CloseToolModal()
    {
        IsToolModalOpen = false;
    }

    private async Task LoadSavedPreparationAsync()
    {
        ErrorMessage = null;
        var saved = await StateStore.GetPreparationStateAsync(StudentEmail);
        if (saved is null)
        {
            ErrorMessage = "No saved preparation found for this email.";
            return;
        }

        Result = saved;
    }

    private void GoToAssessment()
    {
        if (string.IsNullOrWhiteSpace(StudentEmail))
        {
            ErrorMessage = "Student email is required to start the assessment.";
            return;
        }

        var encodedEmail = Uri.EscapeDataString(StudentEmail.Trim());
        NavigationManager.NavigateTo($"/assessment?email={encodedEmail}");
    }

    private static string MarkdownToHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return Markdown.ToHtml(text, pipeline);
    }
}