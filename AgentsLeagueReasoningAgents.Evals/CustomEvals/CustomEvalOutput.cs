using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace AgentsLeagueReasoningAgents.Evals.CustomEvals;

public class CustomEvalOutput
{
    [Description("Step-by-step reasoning for the evaluations")]
    public string Reasoning { get; set; }
    [Description("Positive and Negative examples from the output to that justify the scores")]
    public string Examples { get; set; }
    [Description("Score for the Learning Path evaluation (1-5)")]
    public int LearningPathScore { get; set; }
    [Description("Score for the Study Plan evaluation (1-5)")]
    public int StudyPlanScore { get; set; }
    [Description("Score for the Engagement Plan evaluation (1-5)")]
    public int EngagementPlanScore { get; set; }
    [Description("Overall Score (1-5)")]
    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public class SchemaGen
{
    public static string GenerateEvalSchema()
    {
        JsonSchemaExporterOptions exporterOptions = new()
        {
            TransformSchemaNode = (context, schema) =>
            {
                // Determine if a type or property and extract the relevant attribute provider.
                ICustomAttributeProvider? attributeProvider = context.PropertyInfo is not null
                    ? context.PropertyInfo.AttributeProvider
                    : context.TypeInfo.Type;

                // Look up any description attributes.
                DescriptionAttribute? descriptionAttr = attributeProvider?
                    .GetCustomAttributes(inherit: true)
                    .Select(attr => attr as DescriptionAttribute)
                    .FirstOrDefault(attr => attr is not null);

                // Apply description attribute to the generated schema.
                if (descriptionAttr != null)
                {
                    if (schema is not JsonObject jObj)
                    {
                        // Handle the case where the schema is a Boolean.
                        JsonValueKind valueKind = schema.GetValueKind();
                        schema = jObj = new JsonObject();
                        if (valueKind is JsonValueKind.False)
                        {
                            jObj.Add("not", true);
                        }
                    }

                    jObj.Insert(0, "description", descriptionAttr.Description);
                }

                return schema;
            }
        };
        JsonSerializerOptions options = JsonSerializerOptions.Default;
        JsonNode schema = options.GetJsonSchemaAsNode(typeof(CustomEvalOutput), exporterOptions);
        return schema.ToString();

    }
}