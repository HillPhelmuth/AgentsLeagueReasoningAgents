# Evaluation Datasets

This folder contains eval datasets for all agents created by `PreparationAgentFactory`.
Records are generated from `Datasets/SeedSessions` templates and lightly mutated for scenario coverage.

## Coverage

- Agents:
  - `learning-path-curator`
  - `study-plan-generator`
  - `engagement-agent`
  - `readiness-assessment-agent`
- Required Explain evals:
  - `IntentResolutionExplain`
  - `ToolCallAccuracyExplain`
  - `TaskAdherenceExplain`
  - `RelevanceExplain`
  - `CoherenceExplain`
  - `PerceivedIntelligenceExplain` (non-RAG)
  - `FluencyExplain`
  - `EmpathyExplain`
  - `HelpfulnessExplain`

## Dataset format

Primary format is JSONL (one record per line).

Each record contains:

- trace metadata (`case_id`, `scenario_id`, `agent_name`, `difficulty`, `quality_band`)
- synthetic interaction data (`question`, `context`, `model_answer`, `reference_answer`)
- task/control-plane data (`task_goal`, `relevant_context`, tool traces)
- `explain_inputs` payload with all required inputs for each Explain evaluator

See [common/eval-schema.md](common/eval-schema.md) for the full schema contract.

## Generated files

- `common/scenario-catalog.jsonl`
- `curator/learning-path-curator.explain.jsonl`
- `planner/study-plan-generator.explain.jsonl`
- `engagement/engagement-agent.explain.jsonl`
- `assessment/readiness-assessment-agent.explain.jsonl`

## Regeneration

Run from repo root:

```powershell
pwsh ./AgentsLeagueReasoningAgents.Evals/Datasets/generate-datasets.ps1
```

Or in Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\AgentsLeagueReasoningAgents.Evals\Datasets\generate-datasets.ps1
```

The script is deterministic and recreates all JSONL files with the configured counts.

Default distribution is `30` curator, `30` planner, `20` engagement, and `40` assessment (`120` total).
