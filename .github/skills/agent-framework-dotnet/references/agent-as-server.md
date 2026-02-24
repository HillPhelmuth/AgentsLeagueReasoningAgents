# Agent as HTTP Server Best Practices

Converting an Agent-Framework-based Agent/Workflow/App to run as an HTTP server requires code changes to host the agent as a RESTful HTTP server.

(This doc applies to .NET/C# SDK only)

> ⚠️ **Source of truth:** Use hosting setup from the current sample folders listed in [agent-examples-latest-links.md](agent-examples-latest-links.md).

## Code Changes

### Run Workflow as Agent

Agent Framework provides a way to run a whole workflow as an agent, via appending `.AsAgent()` to the `WorkflowBuilder`, like:

```csharp
var agent = new WorkflowBuilder(startExecutor)
    .AddEdge(...)
  ...
    .Build()
    .AsAgent();
```

Then, ASP.NET Core hosting packages provide a way to run the above agent as an HTTP server and receive user input directly from HTTP requests.

```xml
<!-- .csproj -->
<ItemGroup>
  <!-- Keep versions aligned to the selected latest sample .csproj -->
  <PackageReference Include="Microsoft.Agents.AI" Version="<from-sample>" />
  <PackageReference Include="Microsoft.Agents.Workflows" Version="<from-sample>" />
  <PackageReference Include="Microsoft.Agents.Hosting.AspNetCore" Version="<from-sample>" />
</ItemGroup>
```

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAIAgent("default", agent);

var app = builder.Build();
app.MapOpenAIChatCompletions();
app.MapOpenAIResponses();
app.MapAGUI();
app.MapA2A();
await app.RunAsync();
```

Notes:
- User may or may not have required hosting packages installed; if missing, install them with `dotnet add package` and run `dotnet restore`.
- When changing startup command line, make sure HTTP server mode is the default one (without additional flags), which is better for local debugging and deployment.
- If loading env variables from `.env`, ensure process/environment values can override local defaults in deployed environments.

### Request/Response Requirements

To handle HTTP request as user input, the workflow's starter executor should support chat/message input types, for example:

```csharp
public async Task HandleAsync(IReadOnlyList<ChatMessage> messages, WorkflowContext<IReadOnlyList<ChatMessage>, string> ctx)
{
    ...
}
```

To return agent output via HTTP response, emit workflow output events and/or terminal outputs from handlers.

```csharp
await ctx.YieldOutputAsync("Agent response text");
```

## Notes

- This step focuses on code changes to prepare an HTTP server-based agent, not containerizing or deploying, so no extra deployment files are required.
- Prefer latest Learn/API guidance and avoid stale preview-specific APIs where naming may drift across releases.
