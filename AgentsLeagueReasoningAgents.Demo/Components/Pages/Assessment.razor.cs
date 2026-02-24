using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Workflows;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AgentsLeagueReasoningAgents.Demo.Components.Pages;

public partial class Assessment
{
    [SupplyParameterFromQuery(Name = "email")]
    public string? Email { get; set; }

    private bool IsLoading { get; set; } = true;
    private bool IsSubmitting { get; set; }
    private string? ErrorMessage { get; set; }
    private AssessmentSessionState? Session { get; set; }
    private AssessmentQuestionOutput? CurrentQuestion { get; set; }
    private List<ChatMessage> Messages { get; } = [];
    private ElementReference AssessmentScrollContainer;
    private bool ShouldScrollToBottom { get; set; }
    private string? LastQuestionId { get; set; }
    [Inject] 
    private IAssessmentWorkflowService AssessmentWorkflowService { get; set; } = default!;
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Student email is required.";
            IsLoading = false;
            return;
        }

        var update = await AssessmentWorkflowService.StartAssessmentAsync(Email);
        Session = update.Session;
        UpdateCurrentQuestion(update.CurrentQuestion);
        if (!string.IsNullOrWhiteSpace(update.Message))
        {
            Messages.Add(ChatMessage.Assistant(update.Message));
        }

        if (Session.Questions.Count == 0)
        {
            ErrorMessage = update.Message ?? "No questions were generated for this assessment.";
        }

        IsLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ShouldScrollToBottom)
        {
            return;
        }

        ShouldScrollToBottom = false;
        await JsRuntime.InvokeVoidAsync("assessmentScroll.scrollToBottom", AssessmentScrollContainer);
    }

    private async Task SelectOptionAsync(AssessmentQuestionOutput question, AssessmentOptionOutput option)
    {
        if (Session is null || IsSubmitting)
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            Messages.Add(ChatMessage.User($"{option.OptionId}. {option.Text}"));
            var update = await AssessmentWorkflowService.SubmitAnswerAsync(Session.StudentEmail, question.QuestionId, option.OptionId);
            Session = update.Session;
            UpdateCurrentQuestion(update.CurrentQuestion);

            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                Messages.Add(ChatMessage.Assistant(update.Message));
            }
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private async Task StartFreshAssessmentAsync()
    {
        if (Session is null || IsSubmitting)
        {
            return;
        }

        IsSubmitting = true;
        ErrorMessage = null;
        try
        {
            Messages.Clear();
            var update = await AssessmentWorkflowService.RestartAssessmentAsync(Session.StudentEmail);
            Session = update.Session;
            UpdateCurrentQuestion(update.CurrentQuestion);

            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                Messages.Add(ChatMessage.Assistant(update.Message));
            }

            if (Session.Questions.Count == 0)
            {
                ErrorMessage = update.Message ?? "No questions were generated for this assessment.";
            }
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private void BackToHome()
    {
        NavigationManager.NavigateTo("/");
    }

    private void UpdateCurrentQuestion(AssessmentQuestionOutput? question)
    {
        CurrentQuestion = question;
        var nextQuestionId = question?.QuestionId;
        if (!string.Equals(LastQuestionId, nextQuestionId, StringComparison.Ordinal))
        {
            ShouldScrollToBottom = true;
        }

        LastQuestionId = nextQuestionId;
    }

    private sealed record ChatMessage(bool IsAssistant, string Text)
    {
        public static ChatMessage Assistant(string text) => new(true, text);
        public static ChatMessage User(string text) => new(false, text);
    }
}
