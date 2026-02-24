using System.Text.Json;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentsLeagueReasoningAgents.Workflows;

public interface IAssessmentWorkflowService
{
    Task<AssessmentProgressUpdate> StartAssessmentAsync(string studentEmail, CancellationToken cancellationToken = default);
    Task<AssessmentProgressUpdate> SubmitAnswerAsync(string studentEmail, string questionId, string selectedOptionId, CancellationToken cancellationToken = default);
    Task<AssessmentProgressUpdate> RestartAssessmentAsync(string studentEmail, CancellationToken cancellationToken = default);
}

public sealed class AssessmentWorkflowService(
    IPreparationAgentFactory agentFactory,
    IPreparationAssessmentStateStore stateStore, ILoggerFactory loggerFactory) : IAssessmentWorkflowService
{
    private const int PassingPercentage = 80;
    private ILogger<AssessmentWorkflowService> _logger = loggerFactory.CreateLogger<AssessmentWorkflowService>();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private const string SeedOutputDir =
        @"C:\Users\adamh\source\repos\AgentsLeagueReasoningAgents\AgentsLeagueReasoningAgents.Evals\Datasets\SeedSessions";
    public async Task<AssessmentProgressUpdate> StartAssessmentAsync(string studentEmail, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting assessment for student: {StudentEmail}", studentEmail);
        var normalizedEmail = studentEmail.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new AssessmentProgressUpdate
            {
                Session = new AssessmentSessionState(),
                Message = "Student email is required to start an assessment."
            };
        }

        var existingSession = await stateStore.GetAssessmentSessionAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (existingSession is not null && existingSession.Questions.Count > 0)
        {
            return BuildProgress(existingSession, true, "Resumed your previous assessment session.");
        }

        var preparation = await stateStore.GetPreparationStateAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (preparation is null)
        {
            return new AssessmentProgressUpdate
            {
                Session = new AssessmentSessionState { StudentEmail = normalizedEmail },
                Message = "No preparation output found for this email. Run the preparation workflow first."
            };
        }

        var assessmentAgent = await agentFactory.CreateReadinessAssessmentAgentAsync(cancellationToken).ConfigureAwait(false);
        var prompt = BuildAssessmentPrompt(preparation, normalizedEmail);

        var runOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<AssessmentQuestionSetOutput>()
            }
        };
        var agentsession = await assessmentAgent.CreateSessionAsync();
        var responseText = (await assessmentAgent.RunAsync(prompt, agentsession, options: runOptions, cancellationToken: cancellationToken).ConfigureAwait(false)).Text ?? string.Empty;
        var serialized = await assessmentAgent.SerializeSessionAsync(agentsession);
#if DEBUG
                var outputPath = Path.Combine(SeedOutputDir, $"{assessmentAgent.Name}", $"session_{Guid.NewGuid()}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(Path.Combine(outputPath), JsonSerializer.Serialize(serialized), cancellationToken);
#endif
        var questionSet = DeserializeOrDefault<AssessmentQuestionSetOutput>(responseText);
        if (questionSet is null || questionSet.Questions.Count == 0)
        {
            return new AssessmentProgressUpdate
            {
                Session = new AssessmentSessionState { StudentEmail = normalizedEmail },
                Message = "Assessment generation failed. Please try again."
            };
        }

        var normalizedQuestions = questionSet.Questions
            .Take(10)
            .Select(NormalizeQuestion)
            .ToList();

        var session = new AssessmentSessionState
        {
            StudentEmail = normalizedEmail,
            IntroMessage = string.IsNullOrWhiteSpace(questionSet.IntroMessage)
                ? "Great progress. Let's run a 10-question readiness assessment."
                : questionSet.IntroMessage,
            Questions = normalizedQuestions,
            CurrentQuestionIndex = 0,
            IsCompleted = false,
            TotalQuestions = normalizedQuestions.Count,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        await stateStore.SaveAssessmentSessionAsync(normalizedEmail, session, cancellationToken).ConfigureAwait(false);
        return BuildProgress(session, true, session.IntroMessage);
    }

    public async Task<AssessmentProgressUpdate> SubmitAnswerAsync(string studentEmail, string questionId, string selectedOptionId, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = studentEmail.Trim();
        var session = await stateStore.GetAssessmentSessionAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (session is null || session.Questions.Count == 0)
        {
            return new AssessmentProgressUpdate
            {
                Session = new AssessmentSessionState { StudentEmail = normalizedEmail },
                Message = "Assessment session not found. Start the assessment first."
            };
        }

        if (session.IsCompleted)
        {
            return BuildProgress(session, false, "Assessment already completed.");
        }

        var currentQuestion = session.Questions[session.CurrentQuestionIndex];
        if (!string.Equals(currentQuestion.QuestionId, questionId, StringComparison.OrdinalIgnoreCase))
        {
            return BuildProgress(session, false, "Please answer the current question shown.");
        }

        session.SelectedAnswersByQuestionId[currentQuestion.QuestionId] = selectedOptionId;
        if (string.Equals(currentQuestion.CorrectOptionId, selectedOptionId, StringComparison.OrdinalIgnoreCase))
        {
            session.CorrectAnswers++;
        }

        var answeredCount = session.SelectedAnswersByQuestionId.Count;
        if (answeredCount >= session.TotalQuestions)
        {
            session.IsCompleted = true;
            session.CompletedAtUtc = DateTimeOffset.UtcNow;
            session.ScorePercentage = session.TotalQuestions == 0
                ? 0
                : Math.Round((double)session.CorrectAnswers * 100 / session.TotalQuestions, 2);
            session.IsReadyForExam = session.ScorePercentage >= PassingPercentage;
            session.Feedback = session.IsReadyForExam
                ? $"You scored {session.CorrectAnswers}/{session.TotalQuestions} ({session.ScorePercentage}%). You're ready for exam planning."
                : $"You scored {session.CorrectAnswers}/{session.TotalQuestions} ({session.ScorePercentage}%). Revisit weak topics and retry the assessment.";
        }
        else
        {
            session.CurrentQuestionIndex++;
        }

        await stateStore.SaveAssessmentSessionAsync(normalizedEmail, session, cancellationToken).ConfigureAwait(false);

        var completionMessage = session.IsCompleted
            ? session.Feedback
            : $"Answer recorded. Question {session.CurrentQuestionIndex + 1} of {session.TotalQuestions}.";

        return BuildProgress(session, true, completionMessage);
    }

    public async Task<AssessmentProgressUpdate> RestartAssessmentAsync(string studentEmail, CancellationToken cancellationToken = default)
    {
        await stateStore.ClearAssessmentSessionAsync(studentEmail, cancellationToken).ConfigureAwait(false);
        return await StartAssessmentAsync(studentEmail, cancellationToken).ConfigureAwait(false);
    }

    private static AssessmentProgressUpdate BuildProgress(AssessmentSessionState session, bool answerAccepted, string? message)
    {
        var currentQuestion = session.IsCompleted || session.Questions.Count == 0
            ? null
            : session.Questions[session.CurrentQuestionIndex];

        return new AssessmentProgressUpdate
        {
            Session = session,
            CurrentQuestion = currentQuestion,
            AnswerAccepted = answerAccepted,
            Message = message
        };
    }

    private static AssessmentQuestionOutput NormalizeQuestion(AssessmentQuestionOutput question)
    {
        var normalizedOptions = question.Options
            .Take(4)
            .Select((opt, index) => new AssessmentOptionOutput
            {
                OptionId = string.IsNullOrWhiteSpace(opt.OptionId)
                    ? ((char)('A' + index)).ToString()
                    : opt.OptionId.Trim().ToUpperInvariant(),
                Text = opt.Text
            })
            .ToList();

        if (normalizedOptions.Count == 0)
        {
            normalizedOptions =
            [
                new AssessmentOptionOutput { OptionId = "A", Text = "Not enough options were generated." },
                new AssessmentOptionOutput { OptionId = "B", Text = "Not enough options were generated." },
                new AssessmentOptionOutput { OptionId = "C", Text = "Not enough options were generated." },
                new AssessmentOptionOutput { OptionId = "D", Text = "Not enough options were generated." }
            ];
        }

        var fallbackCorrectOption = normalizedOptions[0].OptionId;

        return new AssessmentQuestionOutput
        {
            QuestionId = string.IsNullOrWhiteSpace(question.QuestionId)
                ? Guid.NewGuid().ToString("N")
                : question.QuestionId,
            Prompt = question.Prompt,
            Options = normalizedOptions,
            CorrectOptionId = normalizedOptions.Any(o => o.OptionId.Equals(question.CorrectOptionId, StringComparison.OrdinalIgnoreCase))
                ? question.CorrectOptionId.Trim().ToUpperInvariant()
                : fallbackCorrectOption,
            Explanation = question.Explanation
        };
    }

    private static string BuildAssessmentPrompt(PreparationWorkflowResult preparation, string studentEmail)
    {
        var preparedJson = JsonSerializer.Serialize(preparation, JsonOptions);
        return $"""
                Student email: {studentEmail}

                Preparation summary as JSON:
                {preparedJson}

                Generate exactly 10 multiple choice questions.
                Use option ids A, B, C, D for every question.
                Return JSON only matching the provided schema.
                """;
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
}
