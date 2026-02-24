# Scoring Policy

This policy defines baseline pass thresholds for Explain-mode evaluations and weighted composite scores.

## Per-metric thresholds

- `RelevanceExplain`: >= 3.6
- `CoherenceExplain`: >= 3.6
- `PerceivedIntelligenceExplain`: >= 3.5
- `FluencyExplain`: >= 3.7
- `EmpathyExplain`: >= 3.2
- `HelpfulnessExplain`: >= 3.7
- `IntentResolutionExplain`: >= 3.6
- `ToolCallAccuracyExplain`: >= 3.5
- `TaskAdherenceExplain`: >= 3.7

Scale assumes 1-5 scoring.

## Weighted composite by profile

### `prep_default`

- `TaskAdherenceExplain`: 0.20
- `IntentResolutionExplain`: 0.15
- `ToolCallAccuracyExplain`: 0.10
- `RelevanceExplain`: 0.15
- `CoherenceExplain`: 0.10
- `PerceivedIntelligenceExplain`: 0.10
- `FluencyExplain`: 0.10
- `EmpathyExplain`: 0.05
- `HelpfulnessExplain`: 0.05

Pass composite: >= 3.65

### `assessment_strict`

- `TaskAdherenceExplain`: 0.22
- `IntentResolutionExplain`: 0.13
- `ToolCallAccuracyExplain`: 0.12
- `RelevanceExplain`: 0.14
- `CoherenceExplain`: 0.10
- `PerceivedIntelligenceExplain`: 0.12
- `FluencyExplain`: 0.08
- `EmpathyExplain`: 0.03
- `HelpfulnessExplain`: 0.06

Pass composite: >= 3.70

## Hard-fail conditions

A case fails regardless of composite if either condition holds:

- malformed output against expected contract for the target agent
- any of these below 3.0: `TaskAdherenceExplain`, `IntentResolutionExplain`, `ToolCallAccuracyExplain`
