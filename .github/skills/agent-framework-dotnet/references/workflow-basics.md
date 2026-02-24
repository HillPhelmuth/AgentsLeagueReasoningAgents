# C# Workflow Basics

Executors, edges, and the WorkflowBuilder API — the foundation for all workflow patterns.

> ⚠️ **Source of truth:** Use the exact Workflows folders listed in [agent-examples-latest-links.md](agent-examples-latest-links.md). If a symbol here differs from a linked sample, use the linked sample.

For more patterns, SEARCH the GitHub repository (github.com/microsoft/agent-framework) to get code snippets like: Agent as Edge, Custom Agent Executor, Workflow as Agent, Reflection, Condition, Switch-Case, Fan-out/Fan-in, Loop, Human in Loop, Concurrent, etc.

## Executor Node Definitions

| Style | When to Use | Example |
|-------|-------------|---------|
| Executor class + handler method | Nodes needing state or lifecycle hooks | `public sealed class MyNode : Executor` |
| Delegate/function executor | Simple stateless steps | `builder.AddExecutor("my_step", async (...) => ...)` |
| Agent-bound executor | Wrapping an existing AI agent | `agent.BindAsExecutor(...)` / sample-provided binding |
| Agent directly | Using agent as a node | Sample-specific agent executor setup |

## Handler Signature

```csharp
Task HandleAsync(TInput input, WorkflowContext<TOut, TWorkflowOut> ctx)
```

- `TInput` = typed input from upstream node
- `ctx.SendMessageAsync(TOut)` → forwards to downstream nodes
- `ctx.YieldOutputAsync(TWorkflowOut)` → yields workflow output (terminal nodes)
- `WorkflowContext<TOut>` = shorthand for no workflow-terminal output

> ⚠️ **Warning:** Previous node output type must match next node input type — check carefully when mixing node styles.

## Code Sample

```csharp
using Microsoft.Agents.Workflows;

var startExecutor = /* build from _Foundational sample */;
var nextExecutor = /* build from _Foundational sample */;

var workflow = new WorkflowBuilder(startExecutor)
    .AddEdge(startExecutor, nextExecutor)
    .Build();

await using var run = await InProcessExecution.StreamAsync(workflow, "hello world");
await foreach (var workflowEvent in run.WatchStreamAsync())
{
    Console.WriteLine(workflowEvent);
}
```
