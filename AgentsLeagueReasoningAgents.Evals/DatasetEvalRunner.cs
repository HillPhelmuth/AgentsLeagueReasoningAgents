using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Models;
using AgentsLeagueReasoningAgents.Workflows;
using HillPhelmuth.SemanticKernel.LlmAsJudgeEvals;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AgentsLeagueReasoningAgents.Evals;

internal sealed class DatasetEvalRunner(
    EvalService evalService,
    RunnerOptions options,
    string datasetRoot, IPreparationAgentFactory agentFactory)
{
    private static readonly string[] DefaultExplainEvals =
    [
        "IntentResolutionExplain",
        //"ToolCallAccuracyExplain",
        "TaskAdherenceExplain",
        "RelevanceExplain",
        "CoherenceExplain",
        //"PerceivedIntelligenceExplain",
        //"FluencyExplain",
        //"EmpathyExplain",
        "HelpfulnessExplain"
    ];

    private static readonly Dictionary<string, double> MetricThresholds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RelevanceExplain"] = 3.6,
        ["CoherenceExplain"] = 3.6,
        ["PerceivedIntelligenceExplain"] = 3.5,
        ["FluencyExplain"] = 3.7,
        ["EmpathyExplain"] = 3.2,
        ["HelpfulnessExplain"] = 3.7,
        ["IntentResolutionExplain"] = 3.6,
        ["ToolCallAccuracyExplain"] = 3.5,
        ["TaskAdherenceExplain"] = 3.7
    };

    private static readonly Dictionary<string, WeightedProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["prep_default"] = new WeightedProfile(3.65, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["TaskAdherenceExplain"] = 0.20,
            ["IntentResolutionExplain"] = 0.15,
            ["ToolCallAccuracyExplain"] = 0.10,
            ["RelevanceExplain"] = 0.15,
            ["CoherenceExplain"] = 0.10,
            ["PerceivedIntelligenceExplain"] = 0.10,
            ["FluencyExplain"] = 0.10,
            ["EmpathyExplain"] = 0.05,
            ["HelpfulnessExplain"] = 0.05
        }),
        ["assessment_strict"] = new WeightedProfile(3.70, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["TaskAdherenceExplain"] = 0.22,
            ["IntentResolutionExplain"] = 0.13,
            ["ToolCallAccuracyExplain"] = 0.12,
            ["RelevanceExplain"] = 0.14,
            ["CoherenceExplain"] = 0.10,
            ["PerceivedIntelligenceExplain"] = 0.12,
            ["FluencyExplain"] = 0.08,
            ["EmpathyExplain"] = 0.03,
            ["HelpfulnessExplain"] = 0.06
        })
    };

    private static readonly HashSet<string> HardFailMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        "TaskAdherenceExplain",
        "IntentResolutionExplain",
        "ToolCallAccuracyExplain"
    };

    private static readonly Dictionary<string, string> AgentGoals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["learning-path-curator"] = "Suggest the most relevant Microsoft Learn learning paths for the student's requested topics.",
        ["study-plan-generator"] = "Convert curated resources into a realistic week-by-week study plan.",
        ["engagement-agent"] = "Generate reminders and accountability nudges aligned to the study plan.",
        ["readiness-assessment-agent"] = "Generate an assessment that evaluates readiness for the target certification exam."
    };

    private static readonly JsonSerializerOptions WorkflowJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<EvalRunReport> RunAsync(CancellationToken cancellationToken)
    {
        var allCases = LoadCases(datasetRoot, options);
        if (allCases.Count == 0)
        {
            throw new InvalidOperationException("No dataset cases found to evaluate.");
        }

        Console.WriteLine($"Loaded {allCases.Count} dataset cases.");
        Console.WriteLine($"Metric max concurrency: {options.MaxConcurrency}");

        var caseResults = new List<CaseEvaluationResult>(allCases.Count);
        var index = 0;
        foreach (var item in allCases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            Console.WriteLine($"[{index}/{allCases.Count}] Evaluating {item.CaseId} ({item.AgentName})...");

            try
            {
                var runtimeExplainInputs = await GenerateExplainInputsAsync(item, agentFactory, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<string> metricNames = item.RequiredEvals.Count > 0 ? item.RequiredEvals : DefaultExplainEvals;
                var metrics = await ExecuteMetricsAsync(item, metricNames, runtimeExplainInputs, cancellationToken).ConfigureAwait(false);

                var profileName = string.IsNullOrWhiteSpace(item.ThresholdProfile) ? "prep_default" : item.ThresholdProfile;
                var profile = Profiles.TryGetValue(profileName, out var weightedProfile)
                    ? weightedProfile
                    : Profiles["prep_default"];

                var metricByName = metrics.ToDictionary(m => m.MetricName, StringComparer.OrdinalIgnoreCase);
                var composite = CalculateComposite(metricByName, profile);

                var hasHardFail = metrics.Any(m => HardFailMetrics.Contains(m.MetricName) && m.Score < 3.0);
                var thresholdMiss = metrics.Any(m => MetricThresholds.TryGetValue(m.MetricName, out var threshold) && m.Score < threshold);
                var passed = !hasHardFail && !thresholdMiss && composite >= profile.PassComposite;
                var failureReason = passed ? null : BuildFailureReason(metrics, composite, profile);

                caseResults.Add(new CaseEvaluationResult
                {
                    CaseId = item.CaseId,
                    AgentName = item.AgentName,
                    ScenarioId = item.ScenarioId,
                    ThresholdProfile = profileName,
                    CompositeScore = composite,
                    Passed = passed,
                    FailureReason = failureReason,
                    Metrics = metrics
                });
            }
            catch (Exception ex)
            {
                var profileName = string.IsNullOrWhiteSpace(item.ThresholdProfile) ? "prep_default" : item.ThresholdProfile;
                Console.Error.WriteLine($"Case {item.CaseId} failed during runtime input generation/evaluation: {ex.Message}");
                caseResults.Add(new CaseEvaluationResult
                {
                    CaseId = item.CaseId,
                    AgentName = item.AgentName,
                    ScenarioId = item.ScenarioId,
                    ThresholdProfile = profileName,
                    CompositeScore = 0,
                    Passed = false,
                    FailureReason = ex.Message,
                    Metrics = []
                });
            }
        }

        return BuildReport(caseResults);
    }

    private async Task<List<MetricEvaluationResult>> ExecuteMetricsAsync(
        DatasetCase item,
        IReadOnlyList<string> metricNames,
        IReadOnlyDictionary<string, Dictionary<string, object>> runtimeExplainInputs,
        CancellationToken cancellationToken)
    {
        if (options.MaxConcurrency <= 1 || metricNames.Count <= 1)
        {
            var sequentialResults = new List<MetricEvaluationResult>(metricNames.Count);
            foreach (var metricName in metricNames)
            {
                var result = await ExecuteMetricAsync(item, metricName, runtimeExplainInputs, cancellationToken).ConfigureAwait(false);
                sequentialResults.Add(result);
            }

            return sequentialResults;
        }

        var results = new MetricEvaluationResult[metricNames.Count];
        Console.WriteLine($"\n----\nMax Concurrency: {options.MaxConcurrency}\n----\n");
        using var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);

        var tasks = metricNames
                .Select((metricName, index) => EvaluateOneMetricAsync(index, metricName, item, semaphore, cancellationToken))
            /*.ToArray()*/;

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToList();

        async Task EvaluateOneMetricAsync(int index, string metricName, DatasetCase currentItem, SemaphoreSlim gate, CancellationToken ct)
        {
            //await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                results[index] = await ExecuteMetricAsync(currentItem, metricName, runtimeExplainInputs, ct).ConfigureAwait(false);
            }
            finally
            {
                //gate.Release();
            }
        }
    }

    private static double CalculateComposite(IReadOnlyDictionary<string, MetricEvaluationResult> metrics, WeightedProfile profile)
    {
        double weightedTotal = 0;
        double weightSum = 0;

        foreach (var weight in profile.Weights)
        {
            if (metrics.TryGetValue(weight.Key, out var metric))
            {
                weightedTotal += metric.Score * weight.Value;
                weightSum += weight.Value;
            }
        }

        if (weightSum <= 0)
        {
            return 0;
        }

        return weightedTotal / weightSum;
    }

    private static string BuildFailureReason(IReadOnlyList<MetricEvaluationResult> metrics, double composite, WeightedProfile profile)
    {
        var reasons = new List<string>();

        var hardFailures = metrics
            .Where(m => HardFailMetrics.Contains(m.MetricName) && m.Score < 3.0)
            .Select(m => $"{m.MetricName}={m.Score.ToString("0.00", CultureInfo.InvariantCulture)}")
            .ToArray();

        if (hardFailures.Length > 0)
        {
            reasons.Add($"Hard-fail metrics (<3.00): {string.Join(", ", hardFailures)}");
        }

        var thresholdMisses = metrics
            .Where(m =>
                MetricThresholds.TryGetValue(m.MetricName, out var threshold)
                && m.Score < threshold
                && !(HardFailMetrics.Contains(m.MetricName) && m.Score < 3.0))
            .Select(m =>
            {
                var threshold = MetricThresholds[m.MetricName];
                return $"{m.MetricName}={m.Score.ToString("0.00", CultureInfo.InvariantCulture)} (<{threshold.ToString("0.00", CultureInfo.InvariantCulture)})";
            })
            .ToArray();

        if (thresholdMisses.Length > 0)
        {
            reasons.Add($"Below threshold: {string.Join(", ", thresholdMisses)}");
        }

        if (composite < profile.PassComposite)
        {
            reasons.Add($"Composite={composite.ToString("0.00", CultureInfo.InvariantCulture)} (<{profile.PassComposite.ToString("0.00", CultureInfo.InvariantCulture)})");
        }

        return reasons.Count > 0
            ? string.Join("; ", reasons)
            : "Failed scoring checks.";
    }

    private async Task<MetricEvaluationResult> ExecuteMetricAsync(DatasetCase item, string metricName, IReadOnlyDictionary<string, Dictionary<string, object>> runtimeExplainInputs, CancellationToken cancellationToken)
    {
        var normalized = NormalizeMetric(metricName);
        var input = CreateInputModel(runtimeExplainInputs, normalized);
        var result = await evalService.ExecuteScorePlusEval(input, new OpenAIPromptExecutionSettings() { Logprobs = false }).ConfigureAwait(false);

        return new MetricEvaluationResult
        {
            MetricName = normalized,
            EvalName = result.EvalName,
            Score = result.Score,
            ProbScore = result.ProbScore,
            Reasoning = result.Reasoning,
            ChainOfThought = result.ChainOfThought
        };
    }

    private async Task<Dictionary<string, Dictionary<string, object>>> GenerateExplainInputsAsync(
        DatasetCase item,
        IPreparationAgentFactory factory,
        CancellationToken cancellationToken)
    {
        var runtimeExecution = await ExecuteAgentWithRuntimeQuestionAsync(item, factory, cancellationToken).ConfigureAwait(false);
        var responseText = runtimeExecution.ResponseText;

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException($"Agent '{item.AgentName}' returned an empty response for case '{item.CaseId}'.");
        }

        var context = string.IsNullOrWhiteSpace(item.ScenarioId)
            ? $"Agent: {item.AgentName}."
            : $"Scenario {item.ScenarioId} for agent {item.AgentName}.";

        var goal = AgentGoals.TryGetValue(item.AgentName, out var resolvedGoal)
            ? resolvedGoal
            : "Answer the student request correctly and completely.";

        return new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["RelevanceExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question,
                ["context"] = context
            },
            ["CoherenceExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question
            },
            ["PerceivedIntelligenceExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question,
                ["context"] = context,
                ["rag_mode"] = "non-rag"
            },
            ["FluencyExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question
            },
            ["EmpathyExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question
            },
            ["HelpfulnessExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question
            },
            ["IntentResolutionExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question,
                ["relevantContext"] = context
            },
            ["ToolCallAccuracyExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question,
                ["availableTools"] = runtimeExecution.AvailableTools,
                ["invokedTools"] = runtimeExecution.InvokedTools
            },
            ["TaskAdherenceExplain"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["input"] = responseText,
                ["question"] = runtimeExecution.Question,
                ["goal"] = goal
            }
        };
    }

    private async Task<RuntimeAgentExecution> ExecuteAgentWithRuntimeQuestionAsync(
        DatasetCase item,
        IPreparationAgentFactory factory,
        CancellationToken cancellationToken)
    {
        if (item.AgentName.Equals("learning-path-curator", StringComparison.OrdinalIgnoreCase))
        {
            var curator = await factory.CreateLearningPathCuratorAgentAsync(cancellationToken).ConfigureAwait(false);
            var response = await RunAgentWithPromptAsync(curator, item.Question, BuildRunOptions<LearningPathCurationOutput>(), cancellationToken).ConfigureAwait(false);
            return new RuntimeAgentExecution(item.Question, response.ResponseText, response.AvailableTools, response.InvokedTools);
        }

        var request = BuildPreparationRequest(item);

        var curatorForChain = await factory.CreateLearningPathCuratorAgentAsync(cancellationToken).ConfigureAwait(false);
        var curationPrompt = BuildCurationPrompt(request);
        var curation = await RunAgentWithPromptAsync(curatorForChain, curationPrompt, BuildRunOptions<LearningPathCurationOutput>(), cancellationToken).ConfigureAwait(false);

        if (item.AgentName.Equals("study-plan-generator", StringComparison.OrdinalIgnoreCase))
        {
            var planner = await factory.CreateStudyPlanGeneratorAgentAsync(cancellationToken).ConfigureAwait(false);
            var planPrompt = BuildPlanPrompt(request, curation.ResponseText);
            var plan = await RunAgentWithPromptAsync(planner, planPrompt, BuildRunOptions<StudyPlanOutput>(), cancellationToken).ConfigureAwait(false);
            return new RuntimeAgentExecution(planPrompt, plan.ResponseText, plan.AvailableTools, plan.InvokedTools);
        }

        var plannerForChain = await factory.CreateStudyPlanGeneratorAgentAsync(cancellationToken).ConfigureAwait(false);
        var plannerPromptForChain = BuildPlanPrompt(request, curation.ResponseText);
        var plannerForChainResponse = await RunAgentWithPromptAsync(plannerForChain, plannerPromptForChain, BuildRunOptions<StudyPlanOutput>(), cancellationToken).ConfigureAwait(false);

        if (item.AgentName.Equals("engagement-agent", StringComparison.OrdinalIgnoreCase))
        {
            var engagementAgent = await factory.CreateEngagementAgentAsync(cancellationToken).ConfigureAwait(false);
            var engagementPrompt = BuildEngagementPrompt(request, plannerForChainResponse.ResponseText);
            var engagement = await RunAgentWithPromptAsync(engagementAgent, engagementPrompt, BuildRunOptions<EngagementPlanOutput>(), cancellationToken).ConfigureAwait(false);
            return new RuntimeAgentExecution(engagementPrompt, engagement.ResponseText, engagement.AvailableTools, engagement.InvokedTools);
        }

        if (item.AgentName.Equals("readiness-assessment-agent", StringComparison.OrdinalIgnoreCase))
        {
            var engagementForChain = await factory.CreateEngagementAgentAsync(cancellationToken).ConfigureAwait(false);
            var engagementPromptForChain = BuildEngagementPrompt(request, plannerForChainResponse.ResponseText);
            var engagementForChainResponse = await RunAgentWithPromptAsync(engagementForChain, engagementPromptForChain, BuildRunOptions<EngagementPlanOutput>(), cancellationToken).ConfigureAwait(false);

            var preparationResult = new PreparationWorkflowResult(
                DeserializeOrDefault<LearningPathCurationOutput>(curation.ResponseText),
                DeserializeOrDefault<StudyPlanOutput>(plannerForChainResponse.ResponseText),
                DeserializeOrDefault<EngagementPlanOutput>(engagementForChainResponse.ResponseText),
                "Runtime preparation chain generated for eval input context")
            {
                StudentEmail = request.StudentEmail,
                PreparationCompletedAtUtc = DateTimeOffset.UtcNow
            };

            var assessmentPrompt = BuildAssessmentPrompt(preparationResult, request.StudentEmail);
            var assessmentAgent = await factory.CreateReadinessAssessmentAgentAsync(cancellationToken).ConfigureAwait(false);
            var assessment = await RunAgentWithPromptAsync(assessmentAgent, assessmentPrompt, BuildRunOptions<AssessmentQuestionSetOutput>(), cancellationToken).ConfigureAwait(false);

            return new RuntimeAgentExecution(assessmentPrompt, assessment.ResponseText, assessment.AvailableTools, assessment.InvokedTools);
        }

        throw new InvalidOperationException($"Unsupported agent '{item.AgentName}'.");
    }

    private static PreparationWorkflowRequest BuildPreparationRequest(DatasetCase item)
    {
        var topics = item.Question;
        var weeklyHours = 6;
        var durationWeeks = 8;

        var topicsMatch = Regex.Match(item.Question, @"Student topics:\s*(?<topics>.+)", RegexOptions.IgnoreCase);
        if (topicsMatch.Success)
        {
            topics = topicsMatch.Groups["topics"].Value.Trim();
        }

        var weeklyMatch = Regex.Match(item.Question, @"Weekly study hours:\s*(?<hours>\d+)", RegexOptions.IgnoreCase);
        if (weeklyMatch.Success && int.TryParse(weeklyMatch.Groups["hours"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWeeklyHours))
        {
            weeklyHours = parsedWeeklyHours;
        }

        var durationMatch = Regex.Match(item.Question, @"Duration in weeks:\s*(?<weeks>\d+)", RegexOptions.IgnoreCase);
        if (durationMatch.Success && int.TryParse(durationMatch.Groups["weeks"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDurationWeeks))
        {
            durationWeeks = parsedDurationWeeks;
        }

        var scenarioPattern = Regex.Match(item.Question, @"with\s+(?<hours>\d+)\s+hours/week\s+over\s+(?<weeks>\d+)\s+weeks", RegexOptions.IgnoreCase);
        if (scenarioPattern.Success)
        {
            if (int.TryParse(scenarioPattern.Groups["hours"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedWeeklyHours))
            {
                weeklyHours = parsedWeeklyHours;
            }

            if (int.TryParse(scenarioPattern.Groups["weeks"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedDurationWeeks))
            {
                durationWeeks = parsedDurationWeeks;
            }
        }

        var email = string.IsNullOrWhiteSpace(item.ScenarioId)
            ? $"{item.AgentName}@eval.local"
            : $"{item.ScenarioId}@eval.local";

        return new PreparationWorkflowRequest(topics.Trim(), email, weeklyHours, durationWeeks);
    }

    private static ChatClientAgentRunOptions BuildRunOptions<TOutput>()
    {
        return new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<TOutput>()
            }
        };
    }

    private static string BuildCurationPrompt(PreparationWorkflowRequest request)
    {
        return $"""

                Student topics: {request.Topics}
                Weekly study hours: {request.WeeklyHours}
                Duration in weeks: {request.DurationWeeks}

                Produce JSON only matching the provided schema

                """;
    }

    private static string BuildPlanPrompt(PreparationWorkflowRequest request, string curatedLearningPath)
    {
        return $"""

                Student topics: {request.Topics}
                Weekly study hours: {request.WeeklyHours}
                Duration in weeks: {request.DurationWeeks}

                Curated resources:
                {curatedLearningPath}

                Produce JSON only matching the provided schema
                """;
    }

    private static string BuildEngagementPrompt(PreparationWorkflowRequest request, string studyPlan)
    {
        return $"""

                Student email: {request.StudentEmail}
                Study plan:
                {studyPlan}

                Produce JSON only matching the provided schema
                """;
    }

    private static string BuildAssessmentPrompt(PreparationWorkflowResult preparation, string studentEmail)
    {
        var preparedJson = JsonSerializer.Serialize(preparation, WorkflowJsonOptions);
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
            return JsonSerializer.Deserialize<T>(json, WorkflowJsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static async Task<RuntimeRunResult> RunAgentWithPromptAsync(
        AIAgent agent,
        string prompt,
        ChatClientAgentRunOptions runOptions,
        CancellationToken cancellationToken)
    {
        var session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        var response = await agent.RunAsync(prompt, session, options: runOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        var responseText = response.Text?.Trim() ?? string.Empty;
        var serializedSession = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
        var invokedTools = ExtractInvokedTools(serializedSession);
        var availableTools = invokedTools
            .Select(static tool => tool.TryGetValue("tool", out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new RuntimeRunResult(responseText, availableTools, invokedTools);
    }

    private sealed record RuntimeRunResult(
        string ResponseText,
        IReadOnlyList<string> AvailableTools,
        IReadOnlyList<Dictionary<string, object>> InvokedTools);

    private sealed record RuntimeAgentExecution(
        string Question,
        string ResponseText,
        IReadOnlyList<string> AvailableTools,
        IReadOnlyList<Dictionary<string, object>> InvokedTools);

    private static List<Dictionary<string, object>> ExtractInvokedTools(object serializedSession)
    {
        var trace = new List<Dictionary<string, object>>();
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(serializedSession));

        var orderedCallIds = new List<string>();
        var callById = new Dictionary<string, (string Name, JsonElement? Arguments)>(StringComparer.OrdinalIgnoreCase);
        var resultByCallId = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        Traverse(document.RootElement);

        foreach (var callId in orderedCallIds)
        {
            if (!callById.TryGetValue(callId, out var call))
            {
                continue;
            }

            var invokedTool = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["tool"] = call.Name,
                ["arguments"] = call.Arguments.HasValue
                    ? JsonSerializer.Deserialize<object>(call.Arguments.Value.GetRawText()) ?? new Dictionary<string, object>()
                    : new Dictionary<string, object>()
            };

            if (resultByCallId.TryGetValue(callId, out var result))
            {
                invokedTool["outcome"] = JsonSerializer.Deserialize<object>(result.GetRawText()) ?? string.Empty;
            }

            trace.Add(invokedTool);
        }

        return trace;

        void Traverse(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                string? type = null;
                string? callId = null;
                string? toolName = null;
                JsonElement? arguments = null;
                JsonElement? result = null;

                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("$type"))
                    {
                        type = property.Value.GetString();
                    }
                    else if (property.NameEquals("callId"))
                    {
                        callId = property.Value.GetString();
                    }
                    else if (property.NameEquals("name"))
                    {
                        toolName = property.Value.GetString();
                    }
                    else if (property.NameEquals("arguments"))
                    {
                        arguments = property.Value.Clone();
                    }
                    else if (property.NameEquals("result"))
                    {
                        result = property.Value.Clone();
                    }

                    Traverse(property.Value);
                }

                if (string.Equals(type, "functionCall", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(callId) &&
                    !string.IsNullOrWhiteSpace(toolName))
                {
                    if (!callById.ContainsKey(callId))
                    {
                        orderedCallIds.Add(callId);
                    }

                    callById[callId] = (toolName, arguments);
                }

                if (string.Equals(type, "functionResult", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(callId) &&
                    result.HasValue)
                {
                    resultByCallId[callId] = result.Value;
                }
                return;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Traverse(item);
                }
            }
        }
    }

    private static IInputModel CreateInputModel(IReadOnlyDictionary<string, Dictionary<string, object>> explain, string metricName)
    {
        if (!explain.ContainsKey(metricName))
        {
            throw new InvalidOperationException($"Runtime ExplainInputs is missing payload for metric '{metricName}'.");
        }

        static string GetString(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException($"Missing required explain input '{key}'.");
            }
            return (value is string ? value.ToString() : JsonSerializer.Serialize(value)) ?? string.Empty;
        

            
        }

        static string ToRawJson(Dictionary<string,object> element, string propertyName)
        {
            if (!element.TryGetValue(propertyName, out var prop))
            {
                throw new InvalidOperationException($"Missing required explain input '{propertyName}'.");
            }

            return (prop is string ? prop.ToString() : JsonSerializer.Serialize(prop)) ?? "[]";
        }

        return metricName switch
        {
            "RelevanceExplain" => InputModel.RelevanceExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question"),
                GetString(explain[metricName], "context")),

            "CoherenceExplain" => InputModel.CoherenceExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question")),

            "PerceivedIntelligenceExplain" => InputModel.PerceivedIntelligenceNonRagExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question")),

            "FluencyExplain" => InputModel.FluencyExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question")),

            "EmpathyExplain" => InputModel.EmpathyExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question")),

            "HelpfulnessExplain" => InputModel.HelpfulnessExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question")),

            "IntentResolutionExplain" => InputModel.IntentResolutionExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question"),
                GetString(explain[metricName], "relevantContext")),

            "ToolCallAccuracyExplain" => InputModel.ToolCallAccuracyExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question"),
                ToRawJson(explain[metricName], "availableTools"),
                ToRawJson(explain[metricName], "invokedTools")),

            "TaskAdherenceExplain" => InputModel.TaskAdherenceExplainModel(
                GetString(explain[metricName], "input"),
                GetString(explain[metricName], "question"),
                GetString(explain[metricName], "goal")),

            _ => throw new InvalidOperationException($"Unsupported metric: {metricName}")
        };
    }

    private static string NormalizeMetric(string metricName)
    {
        return metricName.Equals("PerceivedIntelligenceNonRagExplain", StringComparison.OrdinalIgnoreCase)
            ? "PerceivedIntelligenceExplain"
            : metricName;
    }

    private static List<DatasetCase> LoadCases(string datasetRoot, RunnerOptions options)
    {
        var datasetPaths = options.DatasetFiles.Count > 0
            ? options.DatasetFiles.Select(path => Path.GetFullPath(path)).ToArray()
            :
            [
                Path.Combine(datasetRoot, "curator", "learning-path-curator.explain.jsonl"),
                Path.Combine(datasetRoot, "planner", "study-plan-generator.explain.jsonl"),
                Path.Combine(datasetRoot, "engagement", "engagement-agent.explain.jsonl"),
                Path.Combine(datasetRoot, "assessment", "readiness-assessment-agent.explain.jsonl")
            ];

        var loaded = new List<DatasetCase>();

        foreach (var path in datasetPaths)
        {
            //if (!File.Exists(path))
            //{
            //    Console.WriteLine($"Skipping missing dataset file: {path}");
            //    continue;
            //}
            var fileNameOnly = Path.GetFileName(path);
            var perAgentCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in FileHelper.ExtractFromAssembly<string>(fileNameOnly).Split('\n', '\r').Skip(10))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var item = JsonSerializer.Deserialize<DatasetCase>(line, JsonDefaults.CaseInsensitive);
                if (item is null)
                {
                    continue;
                }

                if (options.AgentFilter.Count > 0 && !options.AgentFilter.Contains(item.AgentName))
                {
                    continue;
                }

                if (!perAgentCount.TryGetValue(item.AgentName, out var current))
                {
                    current = 0;
                }

                if (options.MaxCasesPerAgent.HasValue && current >= options.MaxCasesPerAgent.Value)
                {
                    continue;
                }

                perAgentCount[item.AgentName] = current + 1;
                loaded.Add(item);
            }
        }

        return loaded;
    }

    private static EvalRunReport BuildReport(IReadOnlyList<CaseEvaluationResult> caseResults)
    {
        var perAgent = new Dictionary<string, AgentEvaluationReport>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in caseResults.GroupBy(x => x.AgentName, StringComparer.OrdinalIgnoreCase))
        {
            var metricGroups = group
                .SelectMany(x => x.Metrics)
                .GroupBy(x => x.MetricName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new MetricAverageReport
                    {
                        AverageScore = g.Average(x => x.Score),
                        AverageProbScore = g.Average(x => x.ProbScore)
                    },
                    StringComparer.OrdinalIgnoreCase);

            var total = group.Count();
            var passed = group.Count(x => x.Passed);

            perAgent[group.Key] = new AgentEvaluationReport
            {
                TotalCases = total,
                PassedCases = passed,
                PassRate = total == 0 ? 0 : (double)passed / total,
                CompositeAverage = group.Average(x => x.CompositeScore),

                MetricAverages = metricGroups
            };
        }

        var totalCases = caseResults.Count;
        var totalPass = caseResults.Count(x => x.Passed);

        return new EvalRunReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            TotalCases = totalCases,
            TotalPassed = totalPass,
            OverallPassRate = totalCases == 0 ? 0 : (double)totalPass / totalCases,
            PerAgent = perAgent,
            Cases = caseResults
        };
    }

    private sealed record WeightedProfile(double PassComposite, Dictionary<string, double> Weights);
}