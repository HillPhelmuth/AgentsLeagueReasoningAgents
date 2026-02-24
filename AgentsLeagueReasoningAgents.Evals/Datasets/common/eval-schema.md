# Eval Dataset Schema (JSONL)

Each line is one JSON object.

## Core fields

- `case_id` (string): unique id (`<agent>-<NNN>`)
- `scenario_id` (string): link to scenario catalog
- `agent_name` (string): one of factory agent names
- `phase` (string): `preparation` or `assessment`
- `difficulty` (string): `easy`, `medium`, `hard`
- `quality_band` (string): `excellent`, `good`, `mixed`, `poor`
- `topic_family` (string): e.g., `AZ-900`, `AZ-104`, `AI-102`, `SC-900`
- `learner_level` (string): `beginner`, `intermediate`, `advanced`

## Interaction fields

- `question` (string): user prompt/question for the agent
- `context` (string): synthetic context relevant to the question
- `model_answer` (string): synthetic response to evaluate
- `reference_answer` (string): synthetic ideal response baseline
- `task_goal` (string): explicit objective for Task Adherence
- `relevant_context` (string): intent/task relevant context

## Tool-trace fields (for Tool Call Accuracy)

- `available_tools` (array of strings)
- `expected_tool_calls` (array of objects)
  - object fields: `tool`, `arguments`, `reason`
- `invoked_tool_calls` (array of objects)
  - object fields: `tool`, `arguments`, `outcome`

## Explain input bundle

`explain_inputs` contains all required payloads for the selected eval functions:

- `RelevanceExplain`: `{ input, question, context }`
- `CoherenceExplain`: `{ input, question }`
- `PerceivedIntelligenceExplain`: `{ input, question, context, rag_mode }`
  - `rag_mode` is always `non-rag` in this repo
- `FluencyExplain`: `{ input, question }`
- `EmpathyExplain`: `{ input, question }`
- `HelpfulnessExplain`: `{ input, question }`
- `IntentResolutionExplain`: `{ input, question, relevantContext }`
- `ToolCallAccuracyExplain`: `{ input, question, availableTools, invokedTools }`
- `TaskAdherenceExplain`: `{ input, question, goal }`

## Evaluation control fields

- `required_evals` (array of strings): the Explain evals to execute for this case
- `threshold_profile` (string): scoring profile id from `scoring-policy.md`
- `expected_contract` (string): expected output contract (for reference)

## Notes

- Engagement uses object-style schema aligned with workflow output contract (`EngagementPlanOutput`).
- Perceived Intelligence is configured as non-RAG for all synthetic datasets.
- Tool traces are synthetic by design (offline reproducible evaluation).
