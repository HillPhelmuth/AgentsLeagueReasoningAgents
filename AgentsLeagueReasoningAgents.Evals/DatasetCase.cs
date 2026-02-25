using System.Text.Json.Serialization;

namespace AgentsLeagueReasoningAgents.Evals;

public sealed class DatasetCase
{
    [JsonPropertyName("case_id")]
    public string CaseId { get; init; } = string.Empty;

    [JsonPropertyName("scenario_id")]
    public string ScenarioId { get; init; } = string.Empty;

    [JsonPropertyName("agent_name")]
    public string AgentName { get; init; } = string.Empty;

    [JsonPropertyName("workflow_mode")]
    public string WorkflowMode { get; init; } = string.Empty;

    [JsonPropertyName("threshold_profile")]
    public string ThresholdProfile { get; init; } = string.Empty;

    [JsonPropertyName("required_evals")]
    public List<string> RequiredEvals { get; init; } = [];
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("explain_inputs")]
    public Dictionary<string, Dictionary<string,object>> ExplainInputs { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public string SourceDatasetFile { get; set; } = string.Empty;
}