using System.Text.Json;
using AgentsLeagueReasoningAgents.Evals;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Services;
using AgentsLeagueReasoningAgents.Workflows;
using Markdig;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components;
using MSLearnPlatformClient.Abstractions;
using MSLearnPlatformClient.Models.Catalog;
using MSLearnPlatformClient.Services;

namespace AgentsLeagueReasoningAgents.Demo.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IPreparationAssessmentStateStore StateStore { get; set; } = default!;

    private string Topics { get; set; } = "Azure AI Engineer Associate";
    private string StudentEmail { get; set; } = "funnytrumpsmean@gmail.com";
    private int WeeklyHours { get; set; } = 6;
    private int DurationWeeks { get; set; } = 8;
    private bool UseSingleAgent { get; set; }
    private bool IsRunning { get; set; }
    private string? ErrorMessage { get; set; }
    private PreparationWorkflowResult? Result { get; set; }
    [Inject] private ILearnCatalogClient LearnCatalogClient { get; set; } = default!;
    protected override Task OnInitializedAsync()
    {
        PreparationWorkflowService.AgentResponseEmitted += HandleAgentResponseEmitted;
        return base.OnInitializedAsync();
    }

    private string? tempString;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            //learn-bizapps.power-pages-administration,learn-bizapps.power-pages-integration,learn-bizapps.power-pages-authentication-user-management,learn-bizapps.power-pages-maintenance-troubleshooting,learn.wwl.introduction-to-devops,learn-bizapps.application-lifecycle-management-architect,learn-bizapps.introduction-solutions,learn.wwl.introduction-to-github-actions
            //var idstring =
            //    "learn-bizapps.ai-builder-grounded-prompts";
            //var ids = idstring.Split(',').Select(s => s.Trim()).ToArray();
            //var response = await LearnCatalogClient.GetCatalogItemAsync(CatalogItemType.Module, ids[0]);

        }
        await base.OnAfterRenderAsync(firstRender);
    }

    public void Dispose()
    {
        PreparationWorkflowService.AgentResponseEmitted -= HandleAgentResponseEmitted;
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