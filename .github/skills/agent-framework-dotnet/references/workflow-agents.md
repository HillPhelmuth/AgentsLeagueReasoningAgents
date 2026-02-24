# Workflow with Agents and Streaming

Wrap chat agents inside workflow executors and consume streaming events. Use this when building workflows where each node is backed by an AI agent.

> ⚠️ **Source of truth:** Use the exact Workflows/Agents sample folders listed in [agent-examples-latest-links.md](agent-examples-latest-links.md).

> 💡 **Tip:** For Foundry scenarios, use `Azure.Identity` credentials and a Foundry project endpoint.

## Pattern: Writer → Reviewer Pipeline

A Writer agent generates content, then a Reviewer agent finalizes the result. Uses streaming to observe events in real-time.

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.Workflows;

var writer = /* create/bind writer executor exactly as in Agents workflow sample */;
var reviewer = /* create/bind reviewer executor exactly as in Agents workflow sample */;

var workflow = new WorkflowBuilder(writer)
    .AddEdge(writer, reviewer)
    .Build();

await using var run = await InProcessExecution.StreamAsync(
    workflow,
    new ChatMessage(ChatRole.User, "Create a slogan for a new electric SUV."));

await foreach (var workflowEvent in run.WatchStreamAsync())
{
    Console.WriteLine(workflowEvent);
}
```

Sample output:
```
State: InProgress
Output: Drive the Future. Affordable Adventure, Electrified.
State: Idle
```
