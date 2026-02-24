# Foundry Multi-Agent Workflow

Multi-agent loop workflow using Foundry project endpoint. Use this when building workflows with bidirectional edges (loops) and turn-based agent interaction.

> ⚠️ **Source of truth:** Use the exact FoundryAgents and Workflows folders listed in [agent-examples-latest-links.md](agent-examples-latest-links.md).

> ⚠️ **Warning:** Use Foundry project endpoint, NOT Azure OpenAI endpoint.

> 💡 **Tip:** Agent names: alphanumeric + hyphens, start/end alphanumeric, max 63 chars.

## Pattern: Student-Teacher Loop

Two Foundry agents interact in a loop with turn-based control.

```csharp
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.Workflows;

var endpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT")!;
var deployment = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME")!;

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(new Uri(endpoint), credential);
var persistentClient = projectClient.GetPersistentAgentsClient();

var student = await persistentClient.CreateAIAgentAsync(
    model: deployment,
    name: "StudentAgent",
    instructions: "You are Jamie, a student. Answer questions briefly.");

var teacher = await persistentClient.CreateAIAgentAsync(
    model: deployment,
    name: "TeacherAgent",
    instructions: "You are Dr. Smith. Ask ONE simple question at a time.");

var studentExecutor = /* bind student agent as executor (sample pattern) */;
var teacherExecutor = /* bind teacher agent as executor (sample pattern) */;

var workflow = new WorkflowBuilder(teacherExecutor)
    .AddEdge(teacherExecutor, studentExecutor)
    .AddEdge(studentExecutor, teacherExecutor)
    .Build();

await using var run = await InProcessExecution.StreamAsync(workflow, "Start the quiz session.");
await foreach (var evt in run.WatchStreamAsync())
{
    Console.WriteLine(evt);
}
```
