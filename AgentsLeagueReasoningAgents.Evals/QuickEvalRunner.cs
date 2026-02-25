using AgentsLeagueReasoningAgents.Agents;
using AgentsLeagueReasoningAgents.Workflows;
using HillPhelmuth.SemanticKernel.LlmAsJudgeEvals;
using System;
using System.Collections.Generic;
using System.Text;
using AgentsLeagueReasoningAgents.Evals.CustomEvals;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AgentsLeagueReasoningAgents.Evals;

internal class QuickEvalRunner(EvalService evalService)
{
    public async Task<ResultScore> ExecuteCustomEval(PreparationWorkflowRequest request,
        PreparationWorkflowResult result)
    {
       
        var inputModel = new EvalInputModel(new Dictionary<string, object?>()
        {
            ["details"] = request.AsMarkdown(),
            ["learningPath"] = result.CuratedLearningPathStructured,
            ["studyPlan"] = result.StudyPlanStructured,
            ["studySchedule"] = result.EngagementPlanStructured
        });
        var functionYaml = FileHelper.ExtractFromAssembly<string>("CustomPrepEval.yaml");
        var kernel = Kernel.CreateBuilder().Build();
        var function = kernel.CreateFunctionFromPromptYaml(functionYaml);
        evalService.AddEvalFunction("CustomPrepEval", function, true);
        var evalResult =
            await evalService.ExecuteScorePlusEval(inputModel,
               new OpenAIPromptExecutionSettings(){ResponseFormat = typeof(CustomEvalOutput)});
        return evalResult;
    }
}
internal class EvalInputModel(Dictionary<string, object?> explainInputs) : IInputModel
{
    public Dictionary<string, object> ExplainInputs { get; init; } = explainInputs;

    public string FunctionName => "CustomPrepEval";
    public KernelArguments RequiredInputs => new(explainInputs);
}