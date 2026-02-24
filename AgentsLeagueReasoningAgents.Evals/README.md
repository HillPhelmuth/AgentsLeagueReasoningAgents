# AgentsLeagueReasoningAgents.Evals Runner

This project contains the executable eval runner that reads synthetic JSONL datasets and executes Explain metrics using `HillPhelmuth.SemanticKernel.LlmAsJudgeEvals`.

## Runtime ExplainInputs

`explain_inputs` stored in dataset JSONL is no longer the primary metric input source at runtime.

For each case, the runner now:

- creates the mapped agent from `agent_name`
- generates the runtime `question` payload per agent using workflow-style chaining:
	- `learning-path-curator`: uses dataset question
	- `study-plan-generator`: uses prompt including live curator output
	- `engagement-agent`: uses prompt including live study-plan output
	- `readiness-assessment-agent`: uses prompt including a live `PreparationWorkflowResult` summary
- derives `input` from the live response
- derives tool-trace fields (`availableTools`, `invokedTools`) from serialized session function calls/results
- constructs all metric payloads (`RelevanceExplain`, `CoherenceExplain`, etc.) from this runtime output

This makes eval outcomes sensitive to current prompts/workflow/agent behavior.

If runtime input generation fails (including missing required fields for a metric), the case is marked failed and evaluation continues to the next case.

## What it executes

For each dataset case, the runner executes:

- `IntentResolutionExplain`
- `ToolCallAccuracyExplain`
- `TaskAdherenceExplain`
- `RelevanceExplain`
- `CoherenceExplain`
- `PerceivedIntelligenceExplain` (mapped to non-RAG explain model)
- `FluencyExplain`
- `EmpathyExplain`
- `HelpfulnessExplain`

## Required environment variables

Use either set:

- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_DEPLOYMENT`

or:

- `AzureOpenAI__Endpoint`
- `AzureOpenAI__ApiKey`
- `AzureOpenAI__DeploymentName`

## Run

From repository root:

```powershell
dotnet run --project .\AgentsLeagueReasoningAgents.Evals\AgentsLeagueReasoningAgents.Evals.csproj
```

### Useful options

- `--dataset-root <path>` override dataset root (default: `AgentsLeagueReasoningAgents.Evals/Datasets`)
- `--output <path>` write report to specific JSON file
- `--max-cases-per-agent <n>` limit cases for fast checks
- `--max-concurrency <n>` run up to `n` metric evaluations in parallel per case (default: `1`)
- `--agent <name>` filter by agent (repeatable)
- `--dataset-file <path>` evaluate only specific JSONL file (repeatable)

Example smoke run:

```powershell
dotnet run --project .\AgentsLeagueReasoningAgents.Evals\AgentsLeagueReasoningAgents.Evals.csproj -- --max-cases-per-agent 1

# Faster run with parallel metric execution
dotnet run --project .\AgentsLeagueReasoningAgents.Evals\AgentsLeagueReasoningAgents.Evals.csproj -- --max-concurrency 4
```

## Output

- Console per-agent summary (metric averages, composite average, pass rate)
- JSON report under `AgentsLeagueReasoningAgents.Evals/Datasets/reports/` unless `--output` is provided
